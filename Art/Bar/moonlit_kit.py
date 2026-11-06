# Moonlit Tails - shared Blender kit: palette, mesh builder and container shapes.
# Imported by build_bar.py and build_props.py AFTER bpy.ops.wm.read_factory_settings(), because importing creates the materials.
import bpy, bmesh, math
from mathutils import Vector, Matrix, Euler

PI = math.pi

# ---------------------------------------------------------------- materials
def hexcol(h):
    h = h.lstrip('#')
    f = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(f(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)

MATS = {}
MAT_INFO = {}   # written next to each FBX so Unity rebuilds the exact same colours
def M(name, hx, rough=0.6, metal=0.0, emit=0.0, alpha=1.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    c = hexcol(hx)
    p.inputs['Base Color'].default_value = c
    p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal
    if emit > 0:
        p.inputs['Emission Color'].default_value = c
        p.inputs['Emission Strength'].default_value = emit
    if alpha < 1:
        p.inputs['Alpha'].default_value = alpha
        m.surface_render_method = 'BLENDED'
    m.diffuse_color = c
    MATS[name] = m
    MAT_INFO[name] = {"hex": hx, "roughness": rough, "metallic": metal, "emission": emit, "alpha": alpha}
    return m

# palette pulled from the character sheets: navy + gold + teal + cream, pink / lime accents
WALL = M("M_Wall_Navy", "#2f3f72", 0.9)
WALL2 = M("M_Wall_NavyDeep", "#232f5a", 0.9)
WOOD = M("M_Wood_Warm", "#b8784a", 0.55)
WOOD_D = M("M_Wood_Dark", "#6e432d", 0.6)
WOOD_L = M("M_Wood_Light", "#d29a66", 0.55)
PLANK = [M("M_Floor_Plank_A", "#a8704a", 0.7), M("M_Floor_Plank_B", "#9a6643", 0.7), M("M_Floor_Plank_C", "#b67c51", 0.7)]
CREAM = M("M_Cream", "#f4e7c9", 0.6)
GOLD = M("M_Metal_Gold", "#e8b84e", 0.3, 1.0)
STEEL = M("M_Metal_Steel", "#cfd5df", 0.3, 0.9)
TEAL = M("M_Teal", "#3db3ad", 0.45)
TEAL_D = M("M_Teal_Dark", "#25807f", 0.5)
PINK = M("M_Pink", "#ff86ad", 0.6)
LIME = M("M_Lime", "#c7e64c", 0.6)
BLUE = M("M_Blue", "#4a7be0", 0.6)
NAVY = M("M_Navy_Fabric", "#1f2a52", 0.8)
RUBBER = M("M_Rubber_Mat", "#2a2f3c", 0.9)
RED = M("M_Red", "#e5484f", 0.5)
ORANGE = M("M_Orange", "#ffa043", 0.5)
YELLOW = M("M_Yellow", "#ffd84a", 0.5)
WHITE = M("M_White", "#fbf7ef", 0.5)
LEAF = M("M_Leaf", "#5cbf6a", 0.7)
LEAF_D = M("M_Leaf_Dark", "#3e9a57", 0.7)
POT = M("M_Terracotta", "#de7b4f", 0.8)
PAPER = M("M_Paper", "#fff6e0", 0.9)
GLASS = M("M_Glass_Clear", "#e3f6ff", 0.05, 0.0, 0.0, 0.3)
ICE = M("M_Glass_Ice", "#eafcff", 0.05, 0.0, 0.0, 0.65)
E_WARM = M("M_Emit_WarmBulb", "#ffd08a", 0.5, 0, 10)
E_LANT = M("M_Emit_Lantern", "#ffb46b", 0.6, 0, 3)
E_PINK = M("M_Emit_NeonPink", "#ff5fa2", 0.4, 0, 12)
E_TEAL = M("M_Emit_NeonTeal", "#4ff0e6", 0.4, 0, 10)
E_YEL = M("M_Emit_NeonYellow", "#ffe066", 0.4, 0, 10)
E_SKY = M("M_Emit_Sky", "#1e2560", 1, 0, 1.0)
E_SKY2 = M("M_Emit_SkyGlow", "#5a4aa0", 1, 0, 1.0)
E_SKY3 = M("M_Emit_SkyHorizon", "#c46fa8", 1, 0, 1.0)
E_HILL = M("M_Emit_Hills", "#151a3c", 1, 0, 1.0)
E_MOON = M("M_Emit_Moon", "#fff3c8", 1, 0, 4)
E_STAR = M("M_Emit_Star", "#fff8dc", 1, 0, 6)
E_WIN = M("M_Emit_HouseWindow", "#ffcc66", 1, 0, 5)
E_SHELF = M("M_Emit_ShelfGlow", "#ffc77a", 1, 0, 5)
E_FRIDGE = M("M_Emit_FridgeGlow", "#d8f6ff", 1, 0, 2.5)
E_SERVE = M("M_Emit_ServeRing", "#ffe7a0", 0.4, 0, 3)
GL_COL = {k: M("M_Glass_" + k, hx, 0.08, 0, 0, a) for k, hx, a in [
    ("Teal", "#47d1c1", 0.6), ("Amber", "#d98a2b", 0.7), ("Clear2", "#e8f4ff", 0.35), ("Gold", "#f2c14e", 0.65),
    ("Blue", "#3b6cf0", 0.7), ("Red", "#e0344a", 0.75), ("Orange", "#ff8c2e", 0.7), ("Green", "#4fae4f", 0.7),
    ("Purple", "#9a6be0", 0.7), ("Pink", "#ff7eb0", 0.7)]}
LIQ = {k: M("M_Liquid_" + k, hx, 0.1, 0, 0, a) for k, hx, a in [
    ("Orange", "#ffa53a", 0.8), ("Blue", "#4fa6ff", 0.75), ("Pink", "#ff8fbf", 0.75), ("Lime", "#c9ef5a", 0.75)]}

# ---------------------------------------------------------------- mesh builder
class MB:
    def __init__(s):
        s.bm = bmesh.new(); s.mats = []
    def _mi(s, m):
        if m not in s.mats: s.mats.append(m)
        return s.mats.index(m)
    def _part(s, verts, m, bevel=0.0, segs=2):
        idx = s._mi(m)
        for f in {f for v in verts for f in v.link_faces}: f.material_index = idx
        if bevel > 0:
            edges = list({e for v in verts for e in v.link_edges})
            r = bmesh.ops.bevel(s.bm, geom=edges, offset=bevel, segments=segs, profile=0.5,
                                affect='EDGES', clamp_overlap=True)
            for f in r['faces']: f.material_index = idx
    @staticmethod
    def _mat(loc, rot=(0, 0, 0), scale=(1, 1, 1)):
        return Matrix.Translation(loc) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
    def box(s, size, loc, m, bevel=0.0, segs=2, rot=(0, 0, 0)):
        r = bmesh.ops.create_cube(s.bm, size=1.0, matrix=s._mat(loc, rot, size))
        s._part(r['verts'], m, min(bevel, min(size) * 0.45), segs); return s
    def boxr(s, x0, x1, y0, y1, z0, z1, m, bevel=0.0, segs=2):   # box from ranges
        return s.box((x1 - x0, y1 - y0, z1 - z0), ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), m, bevel, segs)
    def cyl(s, r, h, loc, m, segs=16, r2=None, rot=(0, 0, 0), bevel=0.0):  # loc = base centre
        mat = s._mat(loc, rot) @ Matrix.Translation((0, 0, h / 2))
        res = bmesh.ops.create_cone(s.bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r,
                                    radius2=r if r2 is None else r2, depth=h, matrix=mat)
        s._part(res['verts'], m, bevel, 2); return s
    def sphere(s, r, loc, m, u=16, v=8, scale=(1, 1, 1), rot=(0, 0, 0)):
        res = bmesh.ops.create_uvsphere(s.bm, u_segments=u, v_segments=v, radius=r, matrix=s._mat(loc, rot, scale))
        s._part(res['verts'], m); return s
    def lathe(s, prof, loc, m, segs=20, closed=False, rot=(0, 0, 0), arc=None, scale=1.0):
        mat = s._mat(loc, rot)
        full = arc is None
        a0, a1 = (0, 2 * PI) if full else arc
        n = segs if full else segs + 1
        rings, allv = [], []
        for (r, z) in prof:
            r, z = r * scale, z * scale
            if r < 1e-7:
                ring = [s.bm.verts.new(mat @ Vector((0, 0, z)))]
            else:
                ring = [s.bm.verts.new(mat @ Vector((r * math.cos(a0 + (a1 - a0) * i / segs),
                                                     r * math.sin(a0 + (a1 - a0) * i / segs), z))) for i in range(n)]
            rings.append(ring); allv += ring
        pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
        for a, b in pairs:
            for i in range(segs):
                j = (i + 1) % n
                if len(a) == 1 and len(b) == 1: continue
                if len(a) == 1: vs = [a[0], b[j], b[i]]
                elif len(b) == 1: vs = [a[i], a[j], b[0]]
                else: vs = [a[i], a[j], b[j], b[i]]
                try: s.bm.faces.new(vs)
                except ValueError: pass
        s._part(allv, m); return s
    def ring(s, R, w, d, loc, m, segs=32, rot=(0, 0, 0), arc=None):  # rectangular-section torus
        return s.lathe([(R - w, -d), (R + w, -d), (R + w, d), (R - w, d)], loc, m, segs, True, rot, arc)
    def star(s, R, loc, m, t=0.01, rot=(0, 0, 0), pts=5, inner=0.45):
        mat = s._mat(loc, rot)
        top, bot = [], []
        for i in range(pts * 2):
            rr = R if i % 2 == 0 else R * inner
            a = PI / 2 + i * PI / pts
            top.append(s.bm.verts.new(mat @ Vector((rr * math.cos(a), rr * math.sin(a), t))))
            bot.append(s.bm.verts.new(mat @ Vector((rr * math.cos(a), rr * math.sin(a), 0))))
        ct, cb = s.bm.verts.new(mat @ Vector((0, 0, t * 1.8))), s.bm.verts.new(mat @ Vector((0, 0, 0)))
        k = len(top)
        for i in range(k):
            j = (i + 1) % k
            s.bm.faces.new([top[i], top[j], ct]); s.bm.faces.new([bot[j], bot[i], cb])
            s.bm.faces.new([bot[i], bot[j], top[j], top[i]])
        s._part(top + bot + [ct, cb], m); return s
    def finish(s, name, c, loc=(0, 0, 0), rot=(0, 0, 0)):
        me = bpy.data.meshes.new(name)
        s.bm.normal_update(); s.bm.to_mesh(me); s.bm.free()
        for m in s.mats: me.materials.append(m)
        for p in me.polygons: p.use_smooth = True
        try: me.set_sharp_from_angle(angle=math.radians(40))
        except Exception: pass
        ob = bpy.data.objects.new(name, me); c.objects.link(ob)
        ob.location = loc; ob.rotation_euler = rot
        return ob

# ---------------------------------------------------------------- container shapes (shared by the bar and the liquids)
# Profiles are (radius, height) points swept around Z, origin at the bottom centre.
GS = 1.15  # glasses slightly oversized: reads better as cartoon + easier to grab with hand tracking
GLASS_SEGS = 14
GLASS_PROFILES = {
    'Highball': [(0, 0), (.034, 0), (.037, .008), (.039, .15), (.035, .15), (.033, .014), (0, .014)],
    'Rocks': [(0, 0), (.044, 0), (.047, .012), (.048, .09), (.044, .09), (.041, .018), (0, .018)],
    'Martini': [(0, 0), (.042, 0), (.043, .005), (.04, .008), (.007, .014), (.005, .085), (.062, .165), (.058, .167), (0, .092)],
    'Coupe': [(0, 0), (.04, 0), (.04, .006), (.006, .012), (.005, .08), (.025, .084), (.05, .1), (.058, .125), (.054, .126),
              (.046, .104), (.024, .09), (0, .088)],
    'Wine': [(0, 0), (.038, 0), (.038, .006), (.005, .012), (.005, .09), (.03, .1), (.044, .14), (.038, .2), (.035, .2),
             (.04, .142), (.028, .106), (0, .1)],
    'Shot': [(0, 0), (.024, 0), (.026, .004), (.029, .065), (.026, .065), (.022, .012), (0, .012)],
    'Mug': [(0, 0), (.046, 0), (.048, .006), (.048, .14), (.043, .14), (.041, .014), (0, .014)],
}
SHAKER_TIN_PROFILE = [(0, 0), (0.042, 0), (0.044, 0.006), (0.05, 0.16), (0.046, 0.16), (0.04, 0.008), (0, 0.008)]
BLENDER_JAR_PROFILE = [(0, 0), (0.05, 0), (0.085, 0.24), (0.08, 0.24), (0.046, 0.01), (0, 0.01)]

# Liquid volume inside each container: follows the inner wall 0.5 mm inside it, from the inner floor to 2 mm under the rim.
# Same segment count and start angle as the container, so the facets line up and nothing pokes through.
# name: (profile, scale, segments)
LIQUID_SHAPES = {
    'Highball': ([(0, .0145), (.0325, .0145), (.0345, .148), (0, .148)], GS, GLASS_SEGS),
    'Rocks': ([(0, .0185), (.0405, .0185), (.0435, .088), (0, .088)], GS, GLASS_SEGS),
    'Martini': ([(0, .0925), (.0558, .165), (0, .165)], GS, GLASS_SEGS),
    'Coupe': ([(0, .0885), (.0235, .0905), (.0455, .1045), (.0525, .124), (0, .124)], GS, GLASS_SEGS),
    'Wine': ([(0, .1005), (.0275, .1065), (.0395, .142), (.0347, .197), (0, .197)], GS, GLASS_SEGS),
    'Shot': ([(0, .0125), (.0215, .0125), (.0253, .063), (0, .063)], GS, GLASS_SEGS),
    'Mug': ([(0, .0145), (.0405, .0145), (.0425, .138), (0, .138)], GS, GLASS_SEGS),
    'ShakerTin': ([(0, .0085), (.0395, .0085), (.0455, .158), (0, .158)], 1.0, 20),
    'BlenderJar': ([(0, .0105), (.0455, .0105), (.079, .238), (0, .238)], 1.0, 20),
}
