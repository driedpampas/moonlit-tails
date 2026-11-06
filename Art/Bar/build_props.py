# Moonlit Tails - new bar props for the drink system (Blender 4.4)
# Run: blender -b --factory-startup -P build_props.py -- <out_dir> [render 0/1]
# Writes MoonlitProps.fbx (grabbable props + their stands), MoonlitLiquids.fbx (liquid volume inside every container),
# MoonlitProps.blend, MoonlitProps_materials.json, stats and preview renders. Units = metres, every prop's origin
# is its bottom centre. Not integrated into the Unity scene yet.
import bpy, bmesh, math, os, sys, json
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.getcwd()
DO_RENDER = (argv[1] != "0") if len(argv) > 1 else True

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

def _script_dir():
    try: return os.path.dirname(os.path.abspath(__file__))
    except NameError:
        for flag in ('-P', '--python'):
            if flag in sys.argv: return os.path.dirname(os.path.abspath(sys.argv[sys.argv.index(flag) + 1]))
    return os.getcwd()
sys.path.insert(0, _script_dir())
from moonlit_kit import *   # same palette and builder as the bar

def coll(name):
    c = bpy.data.collections.new(name); scene.collection.children.link(c); return c
C_PROP = coll("PROPS_New")
C_STAND = coll("ENV_PropStands")
C_LIQ = coll("LIQUIDS")
C_PREV = coll("_PreviewOnly")

# a few new materials, same style as the palette
CER_CREAM = M("M_Ceramic_Cream", "#f6ecd8", 0.35)
CER_NAVY = M("M_Ceramic_Navy", "#2b3a6b", 0.35)
WHIP = M("M_Whip", "#fffaf0", 0.75)
COCOA = M("M_Cocoa", "#5a3826", 0.95)
MARSH_PINK = M("M_Marsh_Pink", "#ffc4d6", 0.85)
MARSH_WHITE = M("M_Marsh_White", "#fff8ee", 0.85)
TEABAG = M("M_TeaBag_Paper", "#efe3c4", 0.95)
TEA_LEAVES = M("M_TeaBag_Leaves", "#b0864f", 0.95)
SPARK_COAT = M("M_Sparkler_Coat", "#6d6a72", 0.9)
GLITTER = M("M_Emit_Glitter", "#b48cff", 0.3, 0, 2)
LIQUID = M("M_Liquid_Base", "#ffffff", 0.1, 0, 0, 0.75)   # Unity's liquid shader sets the real colour per drink

def place(ob, x, y, z=0.0):
    ob.location = (x, y, z); return ob

def strip(b, p0, p1, w, m):
    """thin bar between two points (gold kintsugi lines, stripes)"""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    rot = d.to_track_quat('Z', 'Y').to_euler()
    b.box((w, w * 0.7, d.length), tuple((p0 + p1) / 2), m, 0, 1, rot=tuple(rot))

def puck(b, r, h, loc, m, segs=12, ch=0.003):
    """cheap rounded disc/cylinder (a chamfered lathe instead of a beveled cylinder)"""
    ch = min(ch, r * 0.4, h * 0.4)
    return b.lathe([(0, 0), (r - ch, 0), (r, ch), (r, h - ch), (r - ch, h), (0, h)], loc, m, segs)

def on_surface(r, z, a, out=0.0):
    return (math.cos(a) * (r + out), math.sin(a) * (r + out), z)

# ================================================================ syrup pumps + rack (night 1)
PUMP_BOTTLE = [(0, 0), (0.032, 0), (0.035, 0.008), (0.035, 0.13), (0.03, 0.15), (0.014, 0.16), (0.014, 0.172), (0, 0.172)]
def pump_bottle(name, glass, stripe, x, y):
    b = MB().lathe(PUMP_BOTTLE, (0, 0, 0), glass, 14)
    b.lathe([(0.0356, 0.04), (0.0362, 0.048), (0.0362, 0.102), (0.0356, 0.11)], (0, 0, 0), CREAM, 14)   # label
    b.lathe([(0.0364, 0.066), (0.0364, 0.084)], (0, 0, 0), stripe, 14)                                    # colour stripe
    puck(b, 0.018, 0.02, (0, 0, 0.162), WHITE, 12)                                                       # collar
    bottle = place(b.finish(name, C_PROP), x, y)
    h = MB()   # the head is its own object, so it can move down when pressed
    h.cyl(0.005, 0.03, (0, 0, 0), WHITE, 8)
    puck(h, 0.017, 0.016, (0, 0, 0.026), WHITE, 12, 0.005)
    h.box((0.042, 0.012, 0.01), (0.026, 0, 0.034), WHITE, 0.003, 1)
    h.cyl(0.0045, 0.012, (0.045, 0, 0.026), WHITE, 6)
    head = h.finish(name + "_PumpHead", C_PROP)
    head.parent = bottle; head.location = (0, 0, 0.182)
    return bottle

PUMPS = [("PROP_Pump_SimpleSyrup", GL_COL["Clear2"], BLUE), ("PROP_Pump_GingerSyrup", GL_COL["Gold"], ORANGE),
         ("PROP_Pump_Honey", GL_COL["Amber"], YELLOW), ("PROP_Pump_Spare", GL_COL["Pink"], PINK)]
for i, (n, g, s) in enumerate(PUMPS):
    pump_bottle(n, g, s, -0.135 + i * 0.09, 0)
b = MB()
b.boxr(-0.19, 0.19, -0.055, 0.055, 0, 0.02, WOOD_L, 0.008, 2)
b.boxr(-0.18, 0.18, -0.059, -0.054, 0.004, 0.016, CREAM, 0.002, 1)            # front label strip
for i, (_, _, s) in enumerate(PUMPS):
    b.cyl(0.006, 0.002, (-0.135 + i * 0.09, -0.06, 0.009), s, 10, rot=(PI / 2, 0, 0))   # colour dot under each bottle
place(b.finish("ENV_PumpRack", C_STAND), 0, 0)

# ================================================================ cloth (night 1)
b = MB()
b.box((0.2, 0.1, 0.012), (0, 0, 0.006), CREAM, 0.005, 2)
b.box((0.19, 0.1, 0.011), (0.004, 0, 0.0175), CREAM, 0.005, 2)
b.cyl(0.0115, 0.1, (-0.096, 0.05, 0.012), CREAM, 10, rot=(PI / 2, 0, 0))     # the fold
for x in (-0.05, 0.03):
    b.box((0.012, 0.102, 0.002), (x, 0, 0.0235), TEAL, 0, 1)
place(b.finish("PROP_Cloth", C_PROP), 0.35, 0)

# ================================================================ ceramic mugs (night 5)
MUG_CERAMIC = [(0, 0), (0.042, 0), (0.045, 0.006), (0.046, 0.095), (0.044, 0.1), (0.04, 0.1), (0.039, 0.012), (0, 0.012)]
def ceramic_mug(name, body, band, x, y):
    b = MB().lathe(MUG_CERAMIC, (0, 0, 0), body, 16)
    b.lathe([(0.0463, 0.078), (0.0466, 0.08), (0.0466, 0.09), (0.0463, 0.092)], (0, 0, 0), band, 16)
    b.ring(0.026, 0.008, 0.008, (0.046, 0, 0.052), body, 12, rot=(PI / 2, 0, 0), arc=(-PI / 2, PI / 2))   # chunky handle
    return place(b.finish(name, C_PROP), x, y)
ceramic_mug("PROP_Mug_Ceramic_Cream", CER_CREAM, TEAL, 0.0, 0.25)
ceramic_mug("PROP_Mug_Ceramic_Navy", CER_NAVY, GOLD, 0.13, 0.25)

# ================================================================ whipped cream (night 5)
b = MB()
b.lathe([(0, 0), (0.028, 0), (0.03, 0.006), (0.03, 0.165), (0.024, 0.185), (0, 0.185)], (0, 0, 0), PINK, 14)
b.lathe([(0.0304, 0.05), (0.0304, 0.12)], (0, 0, 0), CREAM, 14)                       # label band
b.star(0.014, (0, -0.0305, 0.085), GOLD, 0.002, rot=(PI / 2, 0, 0))                    # little star on the label
puck(b, 0.017, 0.022, (0, 0, 0.183), WHITE, 12, 0.004)                                 # head
b.cyl(0.009, 0.035, (0, 0, 0.203), WHITE, 6, r2=0.005)                                 # star nozzle (6 sides)
b.box((0.012, 0.035, 0.007), (0, 0.02, 0.212), WHITE, 0.003, 1, rot=(-0.35, 0, 0))     # trigger lever
place(b.finish("PROP_WhipCan", C_PROP), 0.27, 0.25)

def whip_swirl(name, r, x, y):
    prof = [(0, 0), (r, 0), (r * 1.05, 0.22 * r), (r * 0.82, 0.42 * r), (r * 0.86, 0.55 * r), (r * 0.6, 0.75 * r),
            (r * 0.62, 0.86 * r), (r * 0.36, 1.05 * r), (r * 0.3, 1.18 * r), (0, 1.5 * r)]
    return place(MB().lathe(prof, (0, 0, 0), WHIP, 10).finish(name, C_PROP), x, y)
for i, (n, r) in enumerate((("S", 0.025), ("M", 0.032), ("L", 0.04))):
    whip_swirl("PROP_WhipSwirl_" + n, r, 0.38 + i * 0.1, 0.25)

# ================================================================ cocktail umbrellas (night 5)
def umbrella(name, colour, x, y):
    b = MB()
    bm = b.bm
    R, top, low, n = 0.045, 0.112, 0.094, 8
    ci, wi = b._mi(colour), b._mi(WHITE)
    apex_t, apex_b = bm.verts.new((0, 0, top)), bm.verts.new((0, 0, top - 0.004))
    rim_t = [bm.verts.new((R * math.cos(2 * PI * i / n), R * math.sin(2 * PI * i / n), low)) for i in range(n)]
    rim_b = [bm.verts.new((R * 0.97 * math.cos(2 * PI * i / n), R * 0.97 * math.sin(2 * PI * i / n), low - 0.003)) for i in range(n)]
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new([apex_t, rim_t[i], rim_t[j]]); f.material_index = ci if i % 2 == 0 else wi   # striped top
        g = bm.faces.new([apex_b, rim_b[j], rim_b[i]]); g.material_index = ci                         # underside
        e = bm.faces.new([rim_t[i], rim_b[i], rim_b[j], rim_t[j]]); e.material_index = ci              # thin edge
    b.cyl(0.0018, 0.115, (0, 0, 0), WOOD_L, 5)
    b.sphere(0.003, (0, 0, 0.116), colour, 6, 4)
    return place(b.finish(name, C_PROP), x, y)
for i, (n, c) in enumerate((("Pink", PINK), ("Teal", TEAL), ("Yellow", YELLOW))):
    umbrella("PROP_Umbrella_" + n, c, 0.7 + i * 0.1, 0.25)

# ================================================================ kettle + base (night 6)
b = MB()
b.lathe([(0, 0), (0.07, 0), (0.085, 0.03), (0.088, 0.07), (0.08, 0.12), (0.06, 0.15), (0.045, 0.158), (0, 0.158)], (0, 0, 0), TEAL, 16)
b.lathe([(0.0857, 0.026), (0.0862, 0.03), (0.0862, 0.04), (0.0857, 0.044)], (0, 0, 0), GOLD, 16)   # gold band
puck(b, 0.047, 0.012, (0, 0, 0.152), CREAM, 16, 0.004)                                              # lid
b.sphere(0.014, (0, 0, 0.172), CREAM, 8, 5)                                                        # knob
b.cyl(0.017, 0.105, (0.062, 0, 0.05), TEAL, 10, r2=0.009, rot=(0, 0.85, 0))                        # spout
b.ring(0.042, 0.0095, 0.0095, (-0.08, 0, 0.08), CREAM, 14, rot=(PI / 2, 0, 0), arc=(PI / 2, 3 * PI / 2))   # thick side handle, opposite the spout
place(b.finish("PROP_Kettle", C_PROP), 0.0, 0.55)
b = MB()
puck(b, 0.085, 0.016, (0, 0, 0), NAVY, 16, 0.005)
b.sphere(0.006, (0.06, -0.06, 0.016), E_WARM, 8, 4)                                                # "on" light
b.cyl(0.006, 0.06, (-0.085, 0, 0.008), NAVY, 6, rot=(0, -PI / 2, 0))                              # cord stub
place(b.finish("ENV_KettleBase", C_STAND), 0.0, 0.55)

# ================================================================ teacups + saucers (night 6)
TEACUP = [(0, 0), (0.025, 0), (0.026, 0.006), (0.022, 0.01), (0.03, 0.02), (0.042, 0.045), (0.046, 0.06),
          (0.043, 0.06), (0.039, 0.046), (0.027, 0.022), (0, 0.02)]
SAUCER = [(0, 0), (0.05, 0), (0.065, 0.008), (0.068, 0.012), (0.064, 0.013), (0.05, 0.008), (0.03, 0.006), (0, 0.006)]
def teacup(name, rim, x, y, kintsugi=False):
    b = MB().lathe(TEACUP, (0, 0, 0), WHITE, 16)
    b.lathe([(0.0452, 0.053), (0.0464, 0.0605)], (0, 0, 0), rim, 16)                               # rim band
    # handle: an arc whose ends sit on the sloped cup wall (z 0.03 and z 0.05)
    b.ring(0.0116, 0.0042, 0.0038, (0.0428, 0, 0.0384), WHITE, 10, rot=(PI / 2, 0, 0), arc=(-2.33, 1.511))
    if kintsugi:   # gold mended cracks: the cup Character02 broke, fixed with gold
        a = -1.9
        path = [(0.0458, 0.058, a), (0.043, 0.049, a + 0.13), (0.0395, 0.04, a - 0.05), (0.034, 0.029, a + 0.12)]
        for (r0, z0, a0), (r1, z1, a1) in zip(path, path[1:]):
            strip(b, on_surface(r0, z0, a0, 0.0014), on_surface(r1, z1, a1, 0.0014), 0.003, GOLD)
        strip(b, on_surface(0.043, 0.049, a + 0.13, 0.0014), on_surface(0.0435, 0.05, a + 0.42, 0.0014), 0.0025, GOLD)
    return place(b.finish(name, C_PROP), x, y, 0.006)   # sitting in the saucer's well
def saucer(name, rim, x, y):
    b = MB().lathe(SAUCER, (0, 0, 0), WHITE, 18)
    b.lathe([(0.0655, 0.0095), (0.0675, 0.0125)], (0, 0, 0), rim, 18)
    return place(b.finish(name, C_PROP), x, y)
saucer("PROP_Saucer_Gold", GOLD, 0.2, 0.55); teacup("PROP_Teacup_Kintsugi", GOLD, 0.2, 0.55, True)
saucer("PROP_Saucer_Rose", PINK, 0.36, 0.55); teacup("PROP_Teacup_Rose", PINK, 0.36, 0.55)

# ================================================================ tea bags + box (night 6)
def tea_bag(name, tag, x, y):
    b = MB()
    b.box((0.04, 0.05, 0.008), (0, 0, 0.004), TEABAG, 0.003, 2)
    b.box((0.028, 0.034, 0.001), (0, -0.002, 0.0082), TEA_LEAVES, 0.0005, 1)   # tea showing through the paper
    b.cyl(0.0008, 0.06, (0, 0.025, 0.006), WHITE, 4, rot=(-PI / 2, 0, 0))
    b.box((0.018, 0.022, 0.002), (0, 0.096, 0.002), tag, 0.0008, 1)
    b.box((0.012, 0.006, 0.0022), (0, 0.092, 0.002), WHITE, 0, 1)
    return place(b.finish(name, C_PROP), x, y)
TEAS = [("PROP_TeaBag_Black", ORANGE), ("PROP_TeaBag_Chamomile", YELLOW), ("PROP_TeaBag_Mint", LEAF), ("PROP_TeaBag_Berry", PINK)]
for i, (n, c) in enumerate(TEAS):
    tea_bag(n, c, 0.52 + i * 0.055, 0.5)
b = MB()   # open wooden box the bags sit in
b.boxr(-0.115, 0.115, -0.04, 0.04, 0, 0.006, WOOD_L, 0.002, 1)
for x0 in (-0.115, 0.109):
    b.boxr(x0, x0 + 0.006, -0.04, 0.04, 0, 0.035, WOOD_L, 0.002, 1)
for y0 in (-0.04, 0.034):
    b.boxr(-0.115, 0.115, y0, y0 + 0.006, 0, 0.035, WOOD_L, 0.002, 1)
for x0 in (-0.059, -0.003, 0.053):
    b.boxr(x0, x0 + 0.004, -0.034, 0.034, 0, 0.028, WOOD_L, 0.001, 1)
place(b.finish("ENV_TeaBox", C_STAND), 0.6, 0.62)

# ================================================================ cocoa tin + scoop (night 6)
b = MB()
b.lathe([(0, 0), (0.045, 0), (0.046, 0.004), (0.046, 0.1), (0.043, 0.1), (0.043, 0.085), (0, 0.085)], (0, 0, 0), RED, 16)
b.cyl(0.0425, 0.003, (0, 0, 0.0852), COCOA, 16)
b.lathe([(0.0463, 0.03), (0.0463, 0.072)], (0, 0, 0), CREAM, 16)
b.lathe([(0.0458, 0.094), (0.0466, 0.096), (0.0466, 0.1), (0.0458, 0.101)], (0, 0, 0), GOLD, 16)
place(b.finish("PROP_CocoaTin", C_PROP), 0.85, 0.55)
b = MB()
b.lathe([(0, 0), (0.018, 0), (0.02, 0.018), (0.018, 0.018), (0.016, 0.002), (0, 0.002)], (0, 0, 0), STEEL, 10)
b.box((0.009, 0.075, 0.005), (0, 0.055, 0.014), STEEL, 0.002, 1)
place(b.finish("PROP_CocoaScoop", C_PROP), 0.97, 0.55)

# ================================================================ marshmallows + bowl (night 6)
b = MB().lathe([(0, 0), (0.035, 0), (0.04, 0.006), (0.055, 0.035), (0.052, 0.036), (0.037, 0.009), (0, 0.009)], (0, 0, 0), CER_CREAM, 16)
place(b.finish("ENV_MarshmallowBowl", C_STAND), 1.12, 0.55)
for i in range(6):
    a = i * 2 * PI / 6
    m = puck(MB(), 0.011, 0.02, (0, 0, 0), MARSH_PINK if i % 2 else MARSH_WHITE, 10, 0.004)
    place(m.finish("PROP_Marshmallow_%02d" % (i + 1), C_PROP), 1.12 + 0.024 * math.cos(a), 0.55 + 0.024 * math.sin(a), 0.012)

# ================================================================ glitter shaker + sparklers (night 7)
b = MB()
b.lathe([(0, 0), (0.024, 0), (0.026, 0.005), (0.026, 0.075), (0.022, 0.08), (0, 0.08)], (0, 0, 0), GLASS, 12)
b.cyl(0.0215, 0.045, (0, 0, 0.004), GLITTER, 12)
puck(b, 0.024, 0.02, (0, 0, 0.078), GOLD, 12, 0.004)
b.star(0.011, (0, 0, 0.098), E_YEL, 0.003)
place(b.finish("PROP_GlitterShaker", C_PROP), 0.0, 0.85)
def sparkler(name, x, y):
    b = MB()
    b.cyl(0.0015, 0.17, (0, 0, 0), STEEL, 5)
    b.cyl(0.0035, 0.1, (0, 0, 0.06), SPARK_COAT, 6)
    b.star(0.012, (0, 0.002, 0.172), E_YEL, 0.004, rot=(PI / 2, 0, 0))
    return place(b.finish(name, C_PROP), x, y)
for i in range(3):
    sparkler("PROP_Sparkler_%02d" % (i + 1), 0.1 + i * 0.04, 0.85)

# ================================================================ journal (night 1)
def journal_closed(x, y):
    b = MB()
    b.box((0.15, 0.21, 0.004), (0, 0, 0.002), NAVY, 0.0015, 1)            # back cover
    b.box((0.142, 0.2, 0.022), (0.003, 0, 0.015), PAPER, 0.002, 1)        # pages
    b.box((0.15, 0.21, 0.004), (0, 0, 0.028), NAVY, 0.0015, 1)            # front cover
    b.cyl(0.015, 0.21, (-0.074, 0.105, 0.015), NAVY, 10, rot=(PI / 2, 0, 0))   # rounded spine
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.02, 0.02, 0.005), (sx * 0.065, sy * 0.095, 0.028), GOLD, 0.002, 1)
    b.ring(0.026, 0.004, 0.0012, (0.006, 0.01, 0.0305), GOLD, 16, arc=(0.7, 2 * PI - 0.7))   # crescent moon
    b.star(0.008, (0.026, 0.026, 0.03), GOLD, 0.002)
    b.box((0.008, 0.05, 0.001), (0.03, -0.125, 0.016), PINK, 0, 1)        # ribbon bookmark
    return place(b.finish("PROP_Journal", C_PROP), x, y)
journal_closed(0.35, 0.85)
INK = M("M_Ink", "#7a86b8", 0.9)
b = MB()   # open version for when it's read on the counter
for s in (-1, 1):
    tilt = (0, -s * 0.06, 0)   # pages rise a little toward the spine
    b.box((0.15, 0.21, 0.004), (s * 0.077, 0, 0.002), NAVY, 0.0015, 1, rot=tilt)
    b.box((0.142, 0.2, 0.012), (s * 0.074, 0, 0.01), PAPER, 0.002, 1, rot=tilt)
    for k in range(6):   # written lines
        b.box((0.1, 0.004, 0.0008), (s * 0.074, 0.07 - k * 0.024, 0.0166), INK, 0, 1, rot=tilt)
b.cyl(0.006, 0.21, (0, 0.105, 0.004), NAVY, 8, rot=(PI / 2, 0, 0))
place(b.finish("PROP_Journal_Open", C_PROP), 0.62, 0.85)

# ================================================================ liquid volumes (one per container)
NEW_LIQUIDS = {   # the new containers in this file, same rules as the kit's LIQUID_SHAPES
    'MugCeramic': ([(0, .0125), (.0385, .0125), (.0395, .098), (0, .098)], 1.0, 16),
    'Teacup': ([(0, .0205), (.0265, .0225), (.0385, .0465), (.0418, .058), (0, .058)], 1.0, 16),
}
LIQUIDS = dict(LIQUID_SHAPES, **NEW_LIQUIDS)
for i, (name, (prof, sc, segs)) in enumerate(LIQUIDS.items()):
    ob = MB().lathe(prof, (0, 0, 0), LIQUID, segs, scale=sc).finish("LIQ_" + name, C_LIQ)
    place(ob, -0.4 + (i % 6) * 0.16, -0.45 - (i // 6) * 0.3)

# fit check: every liquid point must be inside its container's inner wall
CONTAINERS = {k: (GLASS_PROFILES[k], GS) for k in GLASS_PROFILES}
CONTAINERS.update({'ShakerTin': (SHAKER_TIN_PROFILE, 1.0), 'BlenderJar': (BLENDER_JAR_PROFILE, 1.0),
                   'MugCeramic': (MUG_CERAMIC, 1.0), 'Teacup': (TEACUP, 1.0)})
def inner_wall(prof):
    """the part of a container profile after its top-most point, i.e. the inside, going down to the floor"""
    top = max(range(len(prof)), key=lambda i: (prof[i][1], -prof[i][0]))
    return prof[top:]
def radius_at(wall, z):
    for (r0, z0), (r1, z1) in zip(wall, wall[1:]):
        lo, hi = min(z0, z1), max(z0, z1)
        if lo - 1e-9 <= z <= hi + 1e-9 and hi > lo:
            return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
    return None
fit_report = []
for name, (prof, sc, segs) in LIQUIDS.items():
    wall = inner_wall(CONTAINERS[name][0])
    worst = min((radius_at(wall, z) - r) for r, z in prof if r > 0 and radius_at(wall, z) is not None)
    fit_report.append("%s: %s (closest gap %.1f mm)" % (name, "OK" if worst > 0 else "POKES OUT", worst * sc * 1000))
print("LIQUID FIT\n  " + "\n  ".join(fit_report))

# ================================================================ stats + export
dg = bpy.context.evaluated_depsgraph_get()
lines, total = [], 0
for ob in sorted(list(C_PROP.objects) + list(C_STAND.objects) + list(C_LIQ.objects), key=lambda o: o.name):
    me = ob.evaluated_get(dg).to_mesh(); me.calc_loop_triangles(); n = len(me.loop_triangles); ob.evaluated_get(dg).to_mesh_clear()
    lines.append("%-30s %6d" % (ob.name, n)); total += n
stats = "\n".join(lines) + "\nTOTAL triangles: %d  (props %d, stands %d, liquids %d)\n" % (
    total, len(C_PROP.objects), len(C_STAND.objects), len(C_LIQ.objects)) + "\nLiquid fit:\n  " + "\n  ".join(fit_report) + "\n"
print(stats)
os.makedirs(OUT, exist_ok=True)
open(os.path.join(OUT, "props_stats.txt"), "w").write(stats)

used = sorted({s.material.name for c in (C_PROP, C_STAND, C_LIQ) for o in c.objects for s in o.material_slots if s.material})
json.dump({k: MAT_INFO[k] for k in used}, open(os.path.join(OUT, "MoonlitProps_materials.json"), "w"), indent=1)

def export(path, collections):
    bpy.ops.object.select_all(action='DESELECT')
    for c in collections:
        for o in c.objects: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='OFF',
                             add_leaf_bones=False, use_custom_props=False)
export(os.path.join(OUT, "MoonlitProps.fbx"), (C_PROP, C_STAND))
export(os.path.join(OUT, "MoonlitLiquids.fbx"), (C_LIQ,))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "MoonlitProps.blend"), compress=True)

# ================================================================ preview renders
if DO_RENDER:
    b = MB()   # a counter-like table and a navy wall, so the props are seen in the bar's colours
    b.boxr(-1.0, 1.6, -1.2, 1.3, -0.04, 0, WOOD, 0.01, 1)
    b.boxr(-1.0, 1.6, 1.3, 1.34, -0.04, 1.2, WALL, 0, 1)
    b.finish("Prev_Table", C_PREV)
    # glasses around their liquids, to see that the liquid sits inside (coloured like drinks)
    tints = [LIQ["Orange"], LIQ["Pink"], LIQ["Blue"], LIQ["Lime"]]
    for i, (name, _) in enumerate(LIQUIDS.items()):
        x, y = -0.4 + (i % 6) * 0.16, -0.45 - (i // 6) * 0.3
        lq = bpy.data.objects["LIQ_" + name]
        lq.data.materials[0] = tints[i % 4]
        prof, sc = CONTAINERS[name]
        # every container drawn as clear glass here (even the ceramic ones) so the liquid inside can be seen
        place(MB().lathe(prof, (0, 0, 0), GLASS, LIQUIDS[name][2], scale=sc).finish("Prev_Container_" + name, C_PREV), x, y)

    def area(name, loc, target, power, hx, size):
        ld = bpy.data.lights.new(name, 'AREA'); ld.energy = power; ld.color = hexcol(hx)[:3]; ld.size = size
        ob = bpy.data.objects.new(name, ld); C_PREV.objects.link(ob); ob.location = loc
        ob.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    area("Key", (0.5, -0.7, 1.3), (0.5, 0.4, 0), 40, "#ffe2b8", 1.6)
    area("Fill", (-0.8, 0.2, 0.8), (0.4, 0.4, 0.1), 14, "#a9b8ff", 1.5)
    area("Rim", (0.5, 1.25, 0.9), (0.5, 0.4, 0.05), 22, "#ffd0e8", 1.0)
    w = bpy.data.worlds.new("World"); scene.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = hexcol("#2a3570"); w.node_tree.nodes['Background'].inputs[1].default_value = 0.4
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.eevee.taa_render_samples = 32
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1000
    scene.view_settings.view_transform = 'Standard'
    scene.use_nodes = True
    nt = scene.node_tree
    rl = nt.nodes.get('Render Layers') or nt.nodes.new('CompositorNodeRLayers')
    comp = nt.nodes.get('Composite') or nt.nodes.new('CompositorNodeComposite')
    gl = nt.nodes.new('CompositorNodeGlare'); gl.glare_type = 'FOG_GLOW'
    nt.links.new(rl.outputs['Image'], gl.inputs['Image']); nt.links.new(gl.outputs['Image'], comp.inputs['Image'])
    shots = [("props_overview", (0.5, -0.55, 1.15), (0.5, 0.42, 0.0), 26),
             ("props_hot_drinks", (0.35, 0.05, 0.42), (0.45, 0.58, 0.05), 32),
             ("props_toppers", (0.55, -0.12, 0.38), (0.6, 0.3, 0.05), 32),
             ("props_pumps_tools", (0.05, -0.42, 0.42), (0.15, 0.05, 0.08), 32),
             ("liquids_fit", (0.0, -1.35, 0.5), (0.0, -0.6, 0.08), 32)]
    for nm, p, t, lens in shots:
        cd = bpy.data.cameras.new(nm); cd.lens = lens; cd.clip_start = 0.01
        co = bpy.data.objects.new("CAM_" + nm, cd); C_PREV.objects.link(co); co.location = p
        co.rotation_euler = (Vector(t) - Vector(p)).to_track_quat('-Z', 'Y').to_euler()
        scene.camera = co
        scene.render.filepath = os.path.join(OUT, "preview_" + nm + ".png")
        bpy.ops.render.render(write_still=True)
        print("rendered", nm)
