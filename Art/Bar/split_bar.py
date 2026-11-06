# Moonlit Tails - split MoonlitBar.blend into one FBX per piece for Unity (Blender 4.4+ / 5.x)
#
# Run (it never saves or changes the .blend):
#   blender -b --factory-startup --disable-autoexec Art/Bar/MoonlitBar.blend --python Art/Bar/split_bar.py -- Unity/Assets/MoonlitTails/Environment/Bar
#
# What it does
# - Every mesh object becomes its own FBX in <out>/<Group>/, grouped like the Blender collections.
# - Modifiers (the window/door boolean cuts) are applied.
# - Objects that were built in world space (origin at the world centre) get their origin moved to their bottom centre,
#   so they rotate and move around themselves.
# - Identical shapes are written once (e.g. PROP_Glass_Highball_01 and _02 share PROP_Glass_Highball.fbx).
# - Architecture and counter pieces are split into their separate parts, so you can move one panel or one slab:
#   per material, up to PART_LIMIT parts stay separate; more than that (tiles, flutes, planks) stay one group.
# - Writes bar_split.json: which object uses which file, and how far its origin moved (for the Unity assembler).
# Markers and lights aren't exported: they stay in the Unity scene as they are.
import bpy, bmesh, json, os, re, sys, hashlib
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else os.path.join(os.getcwd(), "BarSplit"))

# replaced in Unity by Stiv's models (Environment/StivModels): leave them out
REPLACED = {"ENV_Wainscot_Front", "ENV_Wainscot_Left", "ENV_Wainscot_Right", "ENV_Wainscot_BackL", "ENV_Wainscot_BackR",
            "ENV_DoorFrame_L", "ENV_DoorFrame_R", "ENV_WindowSill_0"}
GROUPS = {"ENV_Architecture": "Architecture", "ENV_BarCounters": "BarCounters", "ENV_Decor": "Decor",
          "ENV_NightSky": "NightSky", "ENV_Stools": "Stools", "PROPS_Interactable": "Props"}
SPLIT_PARTS = {"Architecture", "BarCounters"}
PART_LIMIT = 8
KEEP_GROUPED = ("Floor",)   # planks: one group per plank colour, not one object per plank
PROP_KINDS = {"Glass": "Glasses", "Bottle": "Bottles", "Fridge": "Fridge", "Garnish": "Garnish"}

scene = bpy.context.scene
dg = bpy.context.evaluated_depsgraph_get()

def group_of(ob):
    for c in ob.users_collection:
        if c.name in GROUPS:
            g = GROUPS[c.name]
            if g == "Props":
                kind = ob.name.split("_")[1] if ob.name.count("_") >= 1 else ""
                return "Props/" + PROP_KINDS.get(kind, "Tools")
            return g
    return None

def base_name(n):
    return re.sub(r"_\d+$", "", n)

def evaluated_mesh(ob):
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    me.name = ob.name
    return me

def mesh_hash(me):
    h = hashlib.sha1()
    for v in me.vertices:
        h.update(("%.5f,%.5f,%.5f;" % tuple(v.co)).encode())
    for p in me.polygons:
        mat = me.materials[p.material_index].name if p.material_index < len(me.materials) and me.materials[p.material_index] else "-"
        h.update((",".join(map(str, p.vertices)) + ":" + mat + ";").encode())
    return h.hexdigest()

def bottom_centre(points):
    xs = [p.x for p in points]; ys = [p.y for p in points]; zs = [p.z for p in points]
    return Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))

def shift_mesh(me, offset):
    me.transform(Matrix.Translation(-offset))

def loose_part_groups(me):
    """faces of each loose part, grouped by the part's main material: {material: [[face indices], ...]}"""
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    seen, groups = set(), {}
    for f0 in bm.faces:
        if f0.index in seen: continue
        stack, comp = [f0], []
        seen.add(f0.index)
        while stack:
            f = stack.pop(); comp.append(f.index)
            for v in f.verts:
                for g in v.link_faces:
                    if g.index not in seen: seen.add(g.index); stack.append(g)
        mi = max(set(bm.faces[i].material_index for i in comp), key=lambda m: sum(1 for i in comp if bm.faces[i].material_index == m))
        mat = me.materials[mi].name if mi < len(me.materials) and me.materials[mi] else "Part"
        groups.setdefault(mat, []).append(comp)
    bm.free()
    return groups

def sub_mesh(me, faces, name):
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    keep = set(faces)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    out = bpy.data.meshes.new(name)
    bm.to_mesh(out); bm.free()
    for m in me.materials: out.materials.append(m)
    return out

def export(objects, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', add_leaf_bones=False, use_custom_props=False,
                             bake_anim=False)

# ------------------------------------------------------------------ work in a scratch collection so nothing original changes
work = bpy.data.collections.new("_SplitWork"); scene.collection.children.link(work)
for c in bpy.data.collections:
    if c is not work:
        for o in c.objects: o.hide_set(False)

sources = [o for o in bpy.data.objects if o.type == 'MESH' and o.name not in REPLACED and group_of(o)]
print("split: %d mesh objects (%d left out as replaced)" % (len(sources), len(REPLACED)))

# meshes with modifiers applied, origins moved where needed
items = []
for ob in sources:
    me = evaluated_mesh(ob)
    at_origin = ob.location.length < 1e-6 and Vector(ob.rotation_euler).length < 1e-6 and (ob.scale - Vector((1, 1, 1))).length < 1e-6
    shift = Vector((0, 0, 0))
    if at_origin:
        shift = bottom_centre([v.co for v in me.vertices])
        shift_mesh(me, shift)
    items.append({"ob": ob, "mesh": me, "hash": mesh_hash(me), "shift": shift, "group": group_of(ob)})

# one model per unique shape, named after the objects that use it
by_base = {}
for it in items:
    by_base.setdefault((it["group"], base_name(it["ob"].name)), []).append(it)
models = {}   # hash -> model name
for (group, base), its in by_base.items():
    hashes = {it["hash"] for it in its}
    for it in its:
        if it["hash"] in models: continue
        models[it["hash"]] = base if len(hashes) == 1 else it["ob"].name

layout, written = {}, {}
for it in items:
    ob, me, group = it["ob"], it["mesh"], it["group"]
    model = models[it["hash"]]
    rel = "%s/%s.fbx" % (group, model)
    parts = []
    if it["hash"] not in written:
        chunks = []   # (child name, faces)
        if group in SPLIT_PARTS:
            short = model.split("_", 1)[1] if "_" in model else model
            for mat, comps in sorted(loose_part_groups(me).items()):
                mshort = mat[2:] if mat.startswith("M_") else mat
                together = len(comps) > PART_LIMIT or any(k in model for k in KEEP_GROUPED)
                cs = [[f for c in comps for f in c]] if together else comps
                for k, faces in enumerate(cs):
                    chunks.append(("%s_%s" % (short, mshort) + ("" if len(cs) == 1 else "_%02d" % (k + 1)), faces))
        if len(chunks) > 1:
            # an empty at the object's pivot, with each part as a child at its own bottom centre
            root = bpy.data.objects.new(model, None); work.objects.link(root)
            export_objs = [root]
            for nm, faces in chunks:
                sm = sub_mesh(me, faces, nm)
                pivot = bottom_centre([v.co for v in sm.vertices])
                shift_mesh(sm, pivot)
                child = bpy.data.objects.new(nm, sm); work.objects.link(child)
                child.parent = root; child.location = pivot
                export_objs.append(child); parts.append(nm)
        else:
            root = bpy.data.objects.new(model, me); work.objects.link(root)
            export_objs = [root]
        export(export_objs, os.path.join(OUT, rel))
        written[it["hash"]] = (rel, parts)
    rel, parts = written[it["hash"]]
    layout[ob.name] = {"name": ob.name, "model": rel, "group": group, "shift": [round(x, 6) for x in it["shift"]], "parts": parts,
                       "verts": len(me.vertices), "tris": sum(len(p.vertices) - 2 for p in me.polygons)}

with open(os.path.join(OUT, "bar_split.json"), "w") as f:   # a list, so Unity's JsonUtility can read it
    json.dump({"source": "Art/Bar/MoonlitBar.blend", "replaced": sorted(REPLACED),
               "objects": [layout[k] for k in sorted(layout)]}, f, indent=1)
print("split: %d objects -> %d model files in %s" % (len(layout), len(written), OUT))
for rel, parts in sorted(written.values()):
    if parts: print("  %s: %d parts" % (rel, len(parts)))
