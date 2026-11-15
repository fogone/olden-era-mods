# A model made apart (an image-to-3D tool's .glb, textured, standing in the pose of a game unit's bind pose) skinned to
# that unit's skeleton, so that the game's own animations move it:
#
#   python skin_to_game.py <unit.glb> <mesh name> <model.glb> <out.glb> [--view <degrees>] [--uniform] [--skip-bones <bone>...] [--also <other mesh>...]
#
# <unit.glb>: the game unit exported with its skeleton (nodes, skins, inverse bind matrices, the skinned meshes);
# <mesh name>: its skinned mesh the model replaces. The model is turned about the vertical (the best of four quarter
# turns), stretched to the box of that mesh in its bind pose, and each of its vertices given the bone weights of the
# nearest point of the game mesh's surface. The result: the unit's nodes and that skin, the model's mesh in the space of
# the game mesh (as the game's bind poses expect it) with its texture; what `oe-bundle model` makes a skinned mesh of.
# --view: the side the model's picture showed the unit from (render_pose.py's view, a270 -> 270): an image-to-3D tool
# faces the model to the picture's camera (+Z), so it is turned to face that side; without it the best of four quarter
# turns by shape, which may take the back for the front.
# --uniform: scaled alike on all axes to the game mesh's height, its bottom on the game mesh's bottom and centred over it
# (a weapon or a prop: a club for a sword keeps its own thickness).
# --skip-bones: parts of the game mesh the new creature does not have (a bolt lying beside the crossbowman: the bones
# moving them), left out of the fit and the weights.
# --also: other skinned meshes of the unit on the same bones (a rider and his horse) the model covers too: their
# surfaces give weights and the box as well (the model then replaces the first, the others are hidden).
import json, struct, sys, io
import numpy as np, trimesh
from scipy.spatial import cKDTree

def read_glb(path):
    b = open(path, "rb").read()
    n = struct.unpack("<I", b[12:16])[0]
    j = json.loads(b[20:20 + n])
    return j, b[20 + n + 8:]

def accessor(j, binary, i):
    a = j["accessors"][i]; v = j["bufferViews"][a["bufferView"]]
    comps = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]]
    dt = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[a["componentType"]]
    off = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    stride = v.get("byteStride", 0) or np.dtype(dt).itemsize * comps
    raw = np.frombuffer(binary, np.uint8, count=stride * (a["count"] - 1) + np.dtype(dt).itemsize * comps, offset=off)
    rows = np.lib.stride_tricks.as_strided(raw, (a["count"], np.dtype(dt).itemsize * comps), (stride, 1))
    return np.ascontiguousarray(rows).view(dt).reshape(a["count"], comps)

def local(node):
    if "matrix" in node:
        return np.array(node["matrix"], float).reshape(4, 4).T
    t = np.array(node.get("translation", [0, 0, 0]), float)
    x, y, z, w = node.get("rotation", [0, 0, 0, 1])
    s = np.array(node.get("scale", [1, 1, 1]), float)
    r = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                  [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                  [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
    m = np.eye(4); m[:3, :3] = r * s; m[:3, 3] = t
    return m

def world_matrices(j):
    parent = {c: i for i, n in enumerate(j["nodes"]) for c in n.get("children", [])}
    cache = {}
    def w(i):
        if i not in cache:
            cache[i] = (w(parent[i]) if i in parent else np.eye(4)) @ local(j["nodes"][i])
        return cache[i]
    return [w(i) for i in range(len(j["nodes"]))], parent

def main():
    args = sys.argv[1:]
    unit, mesh_name, model_path, out = args[:4]
    also = [a for a in args[args.index("--also") + 1:] if not a.startswith("--")] if "--also" in args else []
    j, binary = read_glb(unit)
    W, parent = world_matrices(j)
    def game_mesh(name):
        node = next(i for i, n in enumerate(j["nodes"]) if "mesh" in n and j["meshes"][n["mesh"]]["name"] == name)
        skin = j["skins"][j["nodes"][node]["skin"]]
        prim = j["meshes"][j["nodes"][node]["mesh"]]["primitives"][0]
        gv = accessor(j, binary, prim["attributes"]["POSITION"]).astype(float)
        gjoints = accessor(j, binary, prim["attributes"]["JOINTS_0"]).astype(int)
        gweights = accessor(j, binary, prim["attributes"]["WEIGHTS_0"]).astype(float)
        # All its parts (one per material), on the same vertices.
        gfaces = np.concatenate([accessor(j, binary, q["indices"]).reshape(-1, 3).astype(int)
                                 for q in j["meshes"][j["nodes"][node]["mesh"]]["primitives"]])
        ibm = accessor(j, binary, skin["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1).astype(float)
        skinmat = np.array([W[k] for k in skin["joints"]]) @ ibm    # per joint: mesh space -> world, at the bind pose
        dense = np.zeros((len(gv), len(skin["joints"])))
        np.add.at(dense, (np.arange(len(gv))[:, None], gjoints), gweights)   # a bone may repeat (with a zero weight)
        dense /= dense.sum(1, keepdims=True)
        G = np.einsum("vab,vb->va", np.einsum("vj,jab->vab", dense, skinmat), np.c_[gv, np.ones(len(gv))])[:, :3]
        return node, skin, ibm, skinmat, dense, G, gfaces
    node, skin, ibm, skinmat, dense, G, gfaces = game_mesh(mesh_name)
    nb = len(skin["joints"])
    skip_names = [a for a in args[args.index("--skip-bones") + 1:] if not a.startswith("--")] if "--skip-bones" in args else []
    if skip_names:
        skip = [i for i, k in enumerate(skin["joints"]) if j["nodes"][k].get("name") in skip_names]
        keep_v = dense[:, skip].sum(1) < 0.5
        index = -np.ones(len(G), int); index[keep_v] = np.arange(keep_v.sum())
        gfaces = index[gfaces[keep_v[gfaces].all(1)]]
        G, dense = G[keep_v], dense[keep_v]
        print(f"  left out: {int((~keep_v).sum())} vertices of {', '.join(skip_names)}")
    print(f"game mesh {mesh_name}: {len(G)} vertices, {nb} bones, bind pose box {G.min(0).round(3)} {G.max(0).round(3)}")
    for name in also:
        _, other, _, _, d2, G2, f2 = game_mesh(name)
        # Its bones by the first mesh's order (the same nodes).
        order = {k: i for i, k in enumerate(skin["joints"])}
        mapped = np.zeros((len(G2), nb))
        for c, k in enumerate(other["joints"]):
            mapped[:, order[k]] += d2[:, c]
        gfaces = np.r_[gfaces, f2 + len(G)]; dense = np.r_[dense, mapped]; G = np.r_[G, G2]
        print(f"  with {name}: {len(G2)} vertices")

    model = trimesh.load(model_path, force="mesh", process=False)
    V = np.asarray(model.vertices, float)
    gl, gh = G.min(0), G.max(0)
    tree_g = cKDTree(G)
    best = None
    view = float(args[args.index("--view") + 1]) if "--view" in args else None
    for quarter in ([round(view / 90) % 4] if view is not None else range(4)):
        a = quarter * np.pi / 2
        R = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])
        P = V @ R.T
        pl, ph = P.min(0), P.max(0)
        if "--uniform" in args:
            k = (gh[1] - gl[1]) / (ph[1] - pl[1])
            P = (P - (pl + ph) / 2) * k + (gl + gh) / 2
            P[:, 1] += gl[1] - P[:, 1].min()
        else:
            P = (P - pl) / (ph - pl) * (gh - gl) + gl
        sub = P[np.random.default_rng(0).choice(len(P), min(4000, len(P)), replace=False)]
        d = tree_g.query(sub)[0].mean() + cKDTree(sub).query(G)[0].mean()
        print(f"  turn {quarter * 90}: fit {d:.5f}")
        if best is None or d < best[0]:
            best = (d, quarter, P)
    _, quarter, P = best
    # Refined: scale and shift on each axis (alike on all with --uniform) to bring the model onto the game surface,
    # matching points both ways, the farthest third of the pairs left out.
    tree_p = None
    for _ in range(0 if "--uniform" in args else 20):
        sub = P[np.random.default_rng(2).choice(len(P), min(6000, len(P)), replace=False)]
        d1, i1 = tree_g.query(sub); tree_p = cKDTree(P); d2, i2 = tree_p.query(G)
        a_src = np.r_[sub[d1 < np.quantile(d1, 0.66)], P[i2][d2 < np.quantile(d2, 0.66)]]
        a_dst = np.r_[G[i1][d1 < np.quantile(d1, 0.66)], G[d2 < np.quantile(d2, 0.66)]]
        scale_k = np.ones(3); shift = np.zeros(3)
        for ax in range(3):
            A = np.c_[a_src[:, ax], np.ones(len(a_src))]
            scale_k[ax], shift[ax] = np.linalg.lstsq(A, a_dst[:, ax], rcond=None)[0]
        P = P * scale_k + shift
    fit = cKDTree(G).query(P[::7])[0].mean()
    print(f"model {model_path}: {len(V)} vertices, turned {quarter * 90} degrees, fit {fit:.4f}")

    # Weights: from points spread over the game mesh's triangles, each with its triangle's weights blended.
    rng = np.random.default_rng(1)
    tri = G[gfaces]
    area = np.linalg.norm(np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]), axis=1) / 2
    count = 200000
    pick = rng.choice(len(gfaces), count, p=area / area.sum())
    u, v = rng.random(count), rng.random(count)
    flip = u + v > 1; u[flip], v[flip] = 1 - u[flip], 1 - v[flip]
    bary = np.c_[1 - u - v, u, v]
    S = np.einsum("sk,ska->sa", bary, tri[pick])
    SW = np.einsum("sk,skj->sj", bary, dense[gfaces[pick]]).astype(np.float32)
    # The nearest points facing the same way: a point across a gap (the thigh's outside by the hand's inside) faces
    # the other way and gives nothing.
    tn = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); tn /= np.linalg.norm(tn, axis=1, keepdims=True) + 1e-12
    SN = tn[pick]
    a = quarter * np.pi / 2
    R = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])
    stretch = np.ptp(P, axis=0) / np.ptp(V @ R.T, axis=0)   # as the model was scaled on each axis
    PN = (np.asarray(model.vertex_normals, float) @ R.T) / stretch
    PN /= np.linalg.norm(PN, axis=1, keepdims=True) + 1e-12
    dist, near = cKDTree(S).query(P, k=24)
    facing = np.einsum("va,vka->vk", PN, SN[near])
    # The game mesh's winding may be either way: the sign that agrees more often.
    if np.median(facing[:, 0]) < 0:
        facing = -facing
    inv = 1 / (dist + 1e-3 * (gh - gl).max()) * (np.clip(facing, 0, 1) ** 2 + 1e-3)
    w = np.einsum("vk,vkj->vj", inv, SW[near]) / inv.sum(1, keepdims=True)
    # A part far from the game surface (an ear where the game unit has a helmet) takes its weights from its own mesh:
    # spread from the vertices near the surface along the model's edges (vertices at one place, split at UV seams,
    # joined).
    size = (gh - gl).max()
    sure = dist[:, 0] < 0.03 * size
    key = np.unique(np.round(P / (size * 1e-4)).astype(np.int64), axis=0, return_inverse=True)[1].reshape(-1)
    n_at = key.max() + 1
    faces_at = key[np.asarray(model.faces)]
    edges = np.r_[faces_at[:, [0, 1]], faces_at[:, [1, 2]], faces_at[:, [2, 0]]]
    from scipy.sparse import coo_matrix, diags
    A = coo_matrix((np.ones(len(edges) * 2), (np.r_[edges[:, 0], edges[:, 1]], np.r_[edges[:, 1], edges[:, 0]])), shape=(n_at, n_at)).tocsr()
    A = diags(1 / np.maximum(A.sum(1).A1, 1)) @ A
    wa = np.zeros((n_at, nb)); np.add.at(wa, key, w); cnt = np.bincount(key, minlength=n_at)[:, None]; wa /= np.maximum(cnt, 1)
    sa = np.zeros(n_at, bool); np.logical_or.at(sa, key, sure)
    fixed = wa[sa].copy()
    for _ in range(200):
        wa = A @ wa; wa[sa] = fixed
    spread = ~sure & (wa[key].sum(1) > 1e-6)
    w[spread] = wa[key][spread] / wa[key][spread].sum(1, keepdims=True)
    print(f"  weights spread over the model to {int(spread.sum())} vertices far from the game surface")
    top = np.argsort(-w, 1)[:, :4]
    tw = np.take_along_axis(w, top, 1); tw /= tw.sum(1, keepdims=True)
    print(f"  weights: max distance to the game surface {dist[:, 0].max():.4f}, mean {dist[:, 0].mean():.5f}")

    # Into the game mesh's space: each vertex by the inverse of its own bind transform.
    full = np.zeros((len(P), nb)); np.put_along_axis(full, top, tw, 1)
    MV = np.einsum("vj,jab->vab", full, skinmat)
    inverse = np.linalg.inv(MV)
    local_p = np.einsum("vab,vb->va", inverse, np.c_[P, np.ones(len(P))])[:, :3]
    n = np.asarray(model.vertex_normals, float)
    a = quarter * np.pi / 2
    R = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])
    scale = np.ptp(P, axis=0) / np.ptp(V @ R.T, axis=0)
    n = (n @ R.T) / scale
    local_n = np.einsum("vab,vb->va", inverse[:, :3, :3], n)
    local_n /= np.linalg.norm(local_n, axis=1, keepdims=True) + 1e-12
    uv = np.asarray(model.visual.uv, float) if getattr(model.visual, "uv", None) is not None else np.zeros((len(V), 2))
    faces = np.asarray(model.faces, np.uint32)
    image = model.visual.material.baseColorTexture if hasattr(model.visual.material, "baseColorTexture") else model.visual.material.image
    png = io.BytesIO(); image.convert("RGB").save(png, "PNG")

    # The output: the unit's nodes and this skin (other meshes dropped), the model's mesh in the game mesh's space.
    blob = bytearray(); views = []; accs = []
    def add(arr, ctype, typ, target=None, minmax=False):
        data = np.ascontiguousarray(arr).tobytes()
        while len(blob) % 4: blob.append(0)
        views.append({"buffer": 0, "byteOffset": len(blob), "byteLength": len(data), **({"target": target} if target else {})})
        blob.extend(data)
        acc = {"bufferView": len(views) - 1, "componentType": ctype, "count": len(arr), "type": typ}
        if minmax: acc["min"] = arr.min(0).tolist(); acc["max"] = arr.max(0).tolist()
        accs.append(acc); return len(accs) - 1
    a_ibm = add(ibm.transpose(0, 2, 1).reshape(-1, 16).astype(np.float32), 5126, "MAT4")
    a_pos = add(local_p.astype(np.float32), 5126, "VEC3", 34962, True)
    a_nrm = add(local_n.astype(np.float32), 5126, "VEC3", 34962)
    a_uv = add(np.c_[uv[:, 0], 1 - uv[:, 1]].astype(np.float32), 5126, "VEC2", 34962)
    a_j = add(top.astype(np.uint16), 5123, "VEC4", 34962)
    a_w = add(tw.astype(np.float32), 5126, "VEC4", 34962)
    a_i = add(faces.reshape(-1), 5125, "SCALAR", 34963)
    data = png.getvalue()
    while len(blob) % 4: blob.append(0)
    views.append({"buffer": 0, "byteOffset": len(blob), "byteLength": len(data)}); blob.extend(data)
    nodes = json.loads(json.dumps(j["nodes"]))
    for i, nd in enumerate(nodes):
        if i != node:
            nd.pop("mesh", None); nd.pop("skin", None)
    nodes[node]["mesh"] = 0; nodes[node]["skin"] = 0
    out_j = {
        "asset": {"version": "2.0", "generator": "olden-era-mods skin_to_game.py"},
        "scene": 0, "scenes": j["scenes"], "nodes": nodes,
        "skins": [{"name": skin.get("name", "skin"), "joints": skin["joints"], "inverseBindMatrices": a_ibm,
                   **({"skeleton": skin["skeleton"]} if "skeleton" in skin else {})}],
        "meshes": [{"name": mesh_name, "primitives": [{"attributes": {"POSITION": a_pos, "NORMAL": a_nrm, "TEXCOORD_0": a_uv,
                    "JOINTS_0": a_j, "WEIGHTS_0": a_w}, "indices": a_i, "material": 0}]}],
        "materials": [{"name": mesh_name, "pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicFactor": 0, "roughnessFactor": 1}}],
        "textures": [{"source": 0}], "images": [{"bufferView": len(views) - 1, "mimeType": "image/png"}],
        "bufferViews": views, "accessors": accs, "buffers": [{"byteLength": len(blob)}],
    }
    js = json.dumps(out_j).encode()
    while len(js) % 4: js += b" "
    while len(blob) % 4: blob.append(0)
    with open(out, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(blob)))
        f.write(struct.pack("<II", len(js), 0x4E4F534A)); f.write(js)
        f.write(struct.pack("<II", len(blob), 0x004E4942)); f.write(blob)
    print(f"written {out}: {len(local_p)} vertices, {len(faces)} triangles")

main()
