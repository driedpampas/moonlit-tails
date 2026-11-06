# Moonlit Tails - VR bar environment generator (Blender 4.4)
# Run: blender -b --factory-startup -P build_bar.py -- <out_dir> [kintsu.blend] [render 0/1]
# Player stands in the bartender pit at the origin facing +Y (Blender). Units = metres.
import bpy, bmesh, math, random, os, sys
from mathutils import Vector, Matrix, Euler

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.dirname(os.path.abspath(__file__))
KINTSU = argv[1] if len(argv) > 1 else ""
DO_RENDER = (argv[2] != "0") if len(argv) > 2 else True
random.seed(7)
PI = math.pi

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- layout constants
RX, YB, YF, H = 4.6, -1.4, 5.0, 3.6     # room half width, back wall, front wall, ceiling
PLAT = 0.5                              # customer floor height (raised so 1 m characters meet the counter)
TOP = 1.02                              # serving / work counter top
TOPB = 0.96                             # back counter top
PX = 1.8                                # pit half width

# ---------------------------------------------------------------- collections
def coll(name, parent=None):
    c = bpy.data.collections.new(name)
    (parent or scene.collection).children.link(c)
    return c
C_ARCH = coll("ENV_Architecture")
C_BAR = coll("ENV_BarCounters")
C_DECO = coll("ENV_Decor")
C_SKY = coll("ENV_NightSky")
C_PROP = coll("PROPS_Interactable")
C_STOOL = coll("ENV_Stools")
C_LGT = coll("LIGHTS")
C_MK = coll("MARKERS")
C_CUT = coll("_Cutters")
C_PREV = coll("_PreviewOnly")

# palette, MB mesh builder and container shapes are shared with build_props.py
def _script_dir():
    try: return os.path.dirname(os.path.abspath(__file__))
    except NameError:
        for flag in ('-P', '--python'):
            if flag in sys.argv: return os.path.dirname(os.path.abspath(sys.argv[sys.argv.index(flag) + 1]))
    return os.getcwd()
sys.path.insert(0, _script_dir())
from moonlit_kit import *   # creates the materials, so it comes after the factory reset above

def marker(name, loc, rot=(0, 0, 0), size=0.15):
    e = bpy.data.objects.new(name, None); C_MK.objects.link(e)
    e.location = loc; e.rotation_euler = rot; e.empty_display_size = size; e.empty_display_type = 'ARROWS'
    return e

def aim(ob, target):
    d = Vector(target) - ob.location
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()

def light(name, typ, loc, watts, hx, size=0.1, target=None):
    ld = bpy.data.lights.new(name, typ); ld.energy = watts; ld.color = hexcol(hx)[:3]
    if typ == 'AREA': ld.size = size
    else: ld.shadow_soft_size = size
    ob = bpy.data.objects.new(name, ld); C_LGT.objects.link(ob); ob.location = loc
    if target: aim(ob, target)
    return ob

def bool_cut(target, cutter):
    mod = target.modifiers.new("Cut_" + cutter.name, 'BOOLEAN'); mod.object = cutter; mod.operation = 'DIFFERENCE'
    mod.solver = 'EXACT'

def curve_mesh(name, cu, c, loc, rot, m):
    tmp = bpy.data.objects.new(name + "_tmp", cu); scene.collection.objects.link(tmp)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp); me.materials.clear(); me.materials.append(m); me.name = name
    ob = bpy.data.objects.new(name, me); c.objects.link(ob); ob.location = loc; ob.rotation_euler = rot
    return ob

def neon_poly(name, pts, closed, loc, rot, m, thick=0.012):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = thick; cu.bevel_resolution = 2
    sp = cu.splines.new('POLY'); sp.points.add(len(pts) - 1)
    for i, p in enumerate(pts): sp.points[i].co = (p[0], p[1], 0, 1)
    sp.use_cyclic_u = closed
    return curve_mesh(name, cu, C_DECO, loc, rot, m)

def neon_text(name, text, loc, size, m, rot):
    cu = bpy.data.curves.new(name, 'FONT'); cu.body = text; cu.size = size
    cu.extrude = 0.015; cu.bevel_depth = 0.0; cu.resolution_u = 2
    cu.align_x = 'CENTER'; cu.align_y = 'CENTER'
    for f in ("C:/Windows/Fonts/segoesc.ttf", "C:/Windows/Fonts/segoeprb.ttf"):
        if os.path.exists(f):
            cu.font = bpy.data.fonts.load(f); break
    return curve_mesh(name, cu, C_DECO, loc, rot, m)

FACE_PLAYER = (PI / 2, 0, 0)        # local XY plane standing up, facing -Y (toward the player)
FACE_FRONT = (PI / 2, 0, PI)        # facing +Y (toward customers)

# ================================================================ ARCHITECTURE
# raised customer floor (+ the side pieces next to the bartender pit)
b = MB()
b.boxr(-RX, RX, 1.07, YF, 0, PLAT - 0.04, WOOD_D)
b.boxr(-RX, -PX, YB, 1.07, 0, PLAT - 0.04, WOOD_D)
b.boxr(PX, RX, YB, 1.07, 0, PLAT - 0.04, WOOD_D)
b.finish("ENV_Platform", C_ARCH)

def planks(name, x0, x1, y0, y1, z, w=0.3):
    b = MB(); n = max(1, int(round((y1 - y0) / w))); w = (y1 - y0) / n
    for i in range(n):
        xs = x0; cuts = sorted(random.uniform(x0 + 0.6, x1 - 0.6) for _ in range(int((x1 - x0) / 2.4)))
        for xe in cuts + [x1]:
            b.box((xe - xs - 0.006, w - 0.008, 0.04), ((xs + xe) / 2, y0 + w * (i + 0.5), z - 0.02),
                  random.choice(PLANK))
            xs = xe
    return b.finish(name, C_ARCH)
planks("ENV_Floor_Lounge", -RX, RX, 1.07, YF, PLAT)
planks("ENV_Floor_SideL", -RX, -PX, YB, 1.07, PLAT)
planks("ENV_Floor_SideR", PX, RX, YB, 1.07, PLAT)
planks("ENV_Floor_Pit", -PX, PX, YB, 1.07, 0.0, 0.24)
MB().box((2.2, 1.1, 0.02), (0, -0.25, 0.01), TEAL_D, 0.01, 1).finish("ENV_Pit_Mat", C_ARCH)

# walls
walls = {}
b = MB(); b.boxr(-RX - 0.2, RX + 0.2, YB - 0.2, YB, 0, H, WALL); walls['back'] = b.finish("ENV_Wall_Back", C_ARCH)
b = MB(); b.boxr(-RX - 0.2, RX + 0.2, YF, YF + 0.2, 0, H, WALL); walls['front'] = b.finish("ENV_Wall_Front", C_ARCH)
b = MB(); b.boxr(-RX - 0.2, -RX, YB - 0.2, YF + 0.2, 0, H, WALL2); walls['left'] = b.finish("ENV_Wall_Left", C_ARCH)
b = MB(); b.boxr(RX, RX + 0.2, YB - 0.2, YF + 0.2, 0, H, WALL2); walls['right'] = b.finish("ENV_Wall_Right", C_ARCH)

# wainscot + rail + baseboard on the customer side walls
def wainscot(name, x0, x1, y0, y1, axis):
    b = MB()
    if axis == 'x':   # runs along x, wall face at y0 / y1
        b.boxr(x0, x1, y0, y1, PLAT, 1.25, WOOD)
        yy = (y0, y1)
        b.boxr(x0, x1, min(yy) - 0.02, max(yy) + 0.02, 1.22, 1.3, CREAM, 0.015)
        b.boxr(x0, x1, min(yy) - 0.01, max(yy) + 0.01, PLAT, 0.6, WOOD_D)
        n = int((x1 - x0) / 0.6)
        for i in range(n):   # raised panels
            cx = x0 + (i + 0.5) * (x1 - x0) / n
            b.box(((x1 - x0) / n - 0.12, 0.03, 0.42), (cx, (y0 + y1) / 2, 0.92), WOOD_L, 0.012, 1)
    else:
        b.boxr(x0, x1, y0, y1, PLAT, 1.25, WOOD)
        b.boxr(min(x0, x1) - 0.02, max(x0, x1) + 0.02, y0, y1, 1.22, 1.3, CREAM, 0.015)
        b.boxr(min(x0, x1) - 0.01, max(x0, x1) + 0.01, y0, y1, PLAT, 0.6, WOOD_D)
        n = int((y1 - y0) / 0.6)
        for i in range(n):
            cy = y0 + (i + 0.5) * (y1 - y0) / n
            b.box((0.03, (y1 - y0) / n - 0.12, 0.42), ((x0 + x1) / 2, cy, 0.92), WOOD_L, 0.012, 1)
    return b.finish(name, C_ARCH)
wainscot("ENV_Wainscot_Front", -RX, RX, YF - 0.03, YF, 'x')
wsl = wainscot("ENV_Wainscot_Left", -RX, -RX + 0.03, YB, YF, 'y')
wsr = wainscot("ENV_Wainscot_Right", RX - 0.03, RX, YB, YF, 'y')
wainscot("ENV_Wainscot_BackL", -RX, -PX, YB, YB + 0.03, 'x')
wainscot("ENV_Wainscot_BackR", PX, RX, YB, YB + 0.03, 'x')

# ceiling + beams
b = MB(); b.boxr(-RX - 0.2, RX + 0.2, YB - 0.2, YF + 0.2, H, H + 0.15, WOOD_D)
for y in (-0.9, 0.5, 1.9, 3.3, 4.6):
    b.box((2 * RX, 0.2, 0.24), (0, y, H - 0.12), WOOD, 0.02, 1)
b.finish("ENV_Ceiling", C_ARCH)

# --- windows (front wall): one big moon window, two side portholes
WIN = [(0.0, 2.3, 0.9), (-2.6, 2.3, 0.55), (2.6, 2.3, 0.55)]
for i, (wx, wz, wr) in enumerate(WIN):
    cut = MB().cyl(wr, 0.8, (0, 0, -0.4), STEEL, 28).finish("CUT_Window_%d" % i, C_CUT, (wx, YF + 0.1, wz), FACE_PLAYER)
    cut.hide_render = True; cut.display_type = 'WIRE'
    bool_cut(walls['front'], cut)
    b = MB()
    b.ring(wr + 0.02, 0.07, 0.13, (0, 0, 0), CREAM, 28)
    b.ring(wr + 0.1, 0.025, 0.05, (0, 0, -0.1), GOLD, 28)
    b.box((2 * wr, 0.04, 0.05), (0, 0, 0), CREAM, 0.01, 1)          # cross mullions (local XY plane)
    b.box((0.04, 2 * wr, 0.05), (0, 0, 0), CREAM, 0.01, 1)
    b.finish("ENV_WindowFrame_%d" % i, C_ARCH, (wx, YF + 0.02, wz), FACE_PLAYER)
    MB().box((2 * wr + 0.3, 0.22, 0.06), (0, 0, 0), WOOD, 0.02).finish("ENV_WindowSill_%d" % i, C_ARCH, (wx, YF - 0.05, wz - wr - 0.06))

# --- side doors (customers enter left / right) with a little vestibule + noren curtain
for side, sx in (("L", -1), ("R", 1)):
    wx = sx * (RX + 0.1)
    c1 = MB().boxr(-0.4, 0.4, -0.5, 0.5, PLAT, 2.1, STEEL).finish("CUT_Door_%s_A" % side, C_CUT, (wx, 2.7, 0))
    c2 = MB().cyl(0.5, 0.8, (0, 0, -0.4), STEEL, 32).finish("CUT_Door_%s_B" % side, C_CUT, (wx, 2.7, 2.1), (0, PI / 2, 0))
    for c in (c1, c2):
        c.hide_render = True; c.display_type = 'WIRE'
        bool_cut(walls['left' if sx < 0 else 'right'], c)
        bool_cut(wsl if sx < 0 else wsr, c)
    b = MB()
    b.boxr(-0.12, 0.12, -0.62, -0.5, PLAT, 2.1, CREAM, 0.02)
    b.boxr(-0.12, 0.12, 0.5, 0.62, PLAT, 2.1, CREAM, 0.02)
    b.lathe([(0.5, -0.12), (0.62, -0.12), (0.62, 0.12), (0.5, 0.12)], (0, 0, 2.1), CREAM, 16, True, (0, PI / 2, 0), (0, PI))
    b.finish("ENV_DoorFrame_%s" % side, C_ARCH, (sx * RX, 2.7, 0))
    vx = sx * (RX + 0.75)
    b = MB()
    b.boxr(-0.55, 0.55, -0.85, 0.85, PLAT - 0.05, PLAT, WOOD_D)
    b.boxr(sx * 0.5, sx * 0.6, -0.85, 0.85, PLAT, 3.0, WALL2)
    b.boxr(-0.55, 0.55, -0.95, -0.85, PLAT, 3.0, WALL2)
    b.boxr(-0.55, 0.55, 0.85, 0.95, PLAT, 3.0, WALL2)
    b.boxr(-0.55, 0.55, -0.95, 0.95, 3.0, 3.1, WALL2)
    b.finish("ENV_Vestibule_%s" % side, C_ARCH, (vx, 2.7, 0))
    b = MB()
    for k, yy in enumerate((-0.25, 0.25)):
        b.box((0.015, 0.48, 0.55), (0, yy, 2.27), NAVY, 0.005, 1)
        b.box((0.02, 0.48, 0.05), (0, yy, 2.08), GOLD, 0.005, 1)
    b.star(0.09, (sx * -0.012, 0.25, 2.3), E_YEL, 0.006, (0, sx * -PI / 2, 0))
    b.cyl(0.012, 1.1, (0, -0.55, 2.55), WOOD, 8, rot=(-PI / 2, 0, 0))
    b.finish("DECO_Noren_%s" % side, C_DECO, (sx * (RX - 0.02), 2.7, 0))
    light("LGT_Vestibule_%s" % side, 'POINT', (vx, 2.7, 2.6), 25, "#ffc58a", 0.1)

# ================================================================ BAR COUNTERS (U shape around the player)
b = MB()
b.boxr(-PX, PX, 0.42, 1.07, 0, TOP - 0.06, WOOD_D)                     # front body
b.boxr(-PX - 0.05, PX + 0.05, 0.36, 1.14, TOP - 0.06, TOP, WOOD, 0.025, 3)  # front top slab
b.boxr(-PX, -1.15, -1.0, 0.42, 0, TOP - 0.06, WOOD_D)                  # left wing
b.boxr(-PX - 0.05, -1.1, -1.0, 0.36, TOP - 0.06, TOP, WOOD, 0.025, 3)
b.boxr(1.15, PX, -1.0, 0.42, 0, TOP - 0.06, WOOD_D)                    # right wing
b.boxr(1.1, PX + 0.05, -1.0, 0.36, TOP - 0.06, TOP, WOOD, 0.025, 3)
b.boxr(-PX, PX, YB, -1.0, 0, TOPB - 0.05, WOOD_D)                      # back counter
b.boxr(-PX - 0.05, PX + 0.05, YB - 0.02, -0.94, TOPB - 0.05, TOPB, WOOD, 0.02, 3)
# customer face: teal fluting, gold trim, kick plate
for i in range(40):
    x = -PX + 0.045 + i * (2 * PX - 0.09) / 39
    b.cyl(0.035, TOP - 0.1 - PLAT - 0.06, (x, 1.07, PLAT + 0.06), TEAL, 6)
b.boxr(-PX, PX, 1.07, 1.1, PLAT, PLAT + 0.06, TEAL_D, 0.01, 1)
b.boxr(-PX - 0.02, PX + 0.02, 1.06, 1.12, TOP - 0.1, TOP - 0.06, GOLD, 0.01, 1)
# player-facing cabinet doors
def doors(b, x0, x1, y, facing, n):
    w = (x1 - x0) / n
    for i in range(n):
        cx = x0 + (i + 0.5) * w
        if facing == 'y':
            b.box((w - 0.05, 0.03, 0.72), (cx, y, 0.45), CREAM, 0.015, 2)
            b.sphere(0.022, (cx + (w / 2 - 0.08) * (1 if i % 2 else -1), y - 0.03, 0.72), GOLD, 10, 6)
        else:
            b.box((0.03, w - 0.05, 0.72), (y, cx, 0.45), CREAM, 0.015, 2)
            b.sphere(0.022, (y + (0.03 if y < 0 else -0.03), cx, 0.72), GOLD, 10, 6)
doors(b, -1.15, 1.15, 0.405, 'y', 4)
doors(b, -1.0, 0.42, -1.135, 'x', 2)
doors(b, -1.0, 0.42, 1.135, 'x', 2)
b.boxr(-1.15, 1.15, 0.38, 0.42, 0, 0.08, RUBBER)                        # toe kick
b.finish("ENV_BarCounter", C_BAR)

# customer foot rail
b = MB()
b.cyl(0.025, 2 * PX, (-PX, 1.27, PLAT + 0.17), GOLD, 12, rot=(0, PI / 2, 0))
for x in (-1.6, -0.55, 0.55, 1.6):
    b.cyl(0.015, 0.2, (x, 1.12, PLAT + 0.17), GOLD, 8, rot=(-PI / 2, 0, 0))
    b.cyl(0.018, 0.17, (x, 1.27, PLAT), GOLD, 8)
b.finish("ENV_FootRail", C_BAR)

# back wall: tiled backsplash, shelves with glow strips, round neon medallion, sign
b = MB()
for row in range(3):
    for col in range(24):
        x = -PX + 0.075 + col * 0.15 + (0.075 if row % 2 else 0)
        if x > PX - 0.05: continue
        b.box((0.14, 0.03, 0.12), (x, YB + 0.015, TOPB + 0.07 + row * 0.13), random.choice([TEAL, TEAL, TEAL_D, CREAM]), 0.012, 1)
for side in (-1, 1):
    for z in (1.5, 1.88, 2.26):
        x0, x1 = (-1.75, -0.6) if side < 0 else (0.6, 1.75)
        b.boxr(x0, x1, YB, YB + 0.24, z - 0.04, z, WOOD_L, 0.012, 1)
        b.boxr(x0 + 0.04, x1 - 0.04, YB + 0.2, YB + 0.22, z - 0.055, z - 0.04, E_SHELF)
        for bx in (x0 + 0.1, x1 - 0.1):
            b.box((0.03, 0.18, 0.08), (bx, YB + 0.1, z - 0.08), GOLD, 0.008, 1)
b.finish("ENV_BackBar_Wall", C_BAR)
shelf_cols = list(GL_COL.values())
for side in (-1, 1):
    for zi, z in enumerate((1.5, 1.88, 2.26)):
        b = MB()
        x0, x1 = (-1.68, -0.67) if side < 0 else (0.67, 1.68)
        x = x0
        while x < x1:
            st = random.choice(['round', 'square', 'tall', 'flask'])
            bottle_s = random.uniform(0.2, 0.3)
            b2 = None
            prof_r = random.uniform(0.03, 0.04)
            segs = {'square': 4, 'flask': 6}.get(st, 8)
            h = bottle_s
            b.lathe([(0, 0), (prof_r, 0), (prof_r, h * .6), (prof_r * .35, h * .78), (prof_r * .3, h * .95), (0, h * .95)],
                    (x, YB + 0.12, z), random.choice(shelf_cols), segs, rot=(0, 0, PI / 4 if segs == 4 else 0))
            b.lathe([(0, h * .25), (prof_r * 1.03, h * .25), (prof_r * 1.03, h * .45), (0, h * .45)], (x, YB + 0.12, z),
                    random.choice([CREAM, WHITE, PINK, YELLOW]), segs, rot=(0, 0, PI / 4 if segs == 4 else 0))
            x += prof_r * 2 + random.uniform(0.02, 0.05)
        b.finish("DECO_ShelfBottles_%s%d" % ("L" if side < 0 else "R", zi + 1), C_DECO)
MB().cyl(0.62, 0.04, (0, 0, 0), NAVY, 40).ring(0.62, 0.035, 0.045, (0, 0, 0.03), GOLD, 40) \
    .finish("DECO_Medallion", C_DECO, (0, YB + 0.01, 1.95), FACE_FRONT)
neon_poly("DECO_Neon_Glass_Back", [(-0.28, 0.25), (0.28, 0.25), (0.02, -0.05), (0.02, -0.3), (0.16, -0.36),
          (-0.16, -0.36), (-0.02, -0.3), (-0.02, -0.05)], True, (0, YB + 0.08, 1.98), FACE_FRONT, E_TEAL, 0.014)
neon_poly("DECO_Neon_Olive", [(math.cos(a) * 0.06 + 0.1, math.sin(a) * 0.06 + 0.12) for a in [i * PI / 6 for i in range(12)]],
          True, (0, YB + 0.08, 1.98), FACE_FRONT, E_YEL, 0.012)
neon_text("DECO_Neon_Sign_Back", "Moonlit Tails", (0, YB + 0.06, 2.85), 0.42, E_PINK, FACE_FRONT)
light("LGT_Neon_Back", 'POINT', (0, YB + 0.5, 2.7), 30, "#ff6fae", 0.3)
for sx in (-1, 1):
    light("LGT_ShelfGlow_%s" % ("L" if sx < 0 else "R"), 'POINT', (sx * 1.18, YB + 0.45, 1.9), 25, "#ffc77a", 0.3)

# ================================================================ STATIONS & PROPS (all in reach of a standing player)
def make_glass(kind, name, loc, c=C_PROP):
    b = MB().lathe(GLASS_PROFILES[kind], (0, 0, 0), GLASS, GLASS_SEGS, scale=GS)
    if kind == 'Mug':
        b.ring(0.035 * GS, 0.008 * GS, 0.012 * GS, (0.05 * GS, 0, 0.075 * GS), GLASS, 16, rot=(PI / 2, 0, 0), arc=(-PI / 2, PI / 2))
    return b.finish(name, c, loc)

# left wing: glass station on a two-step riser
b = MB()
b.boxr(-1.78, -1.5, -0.95, 0.32, TOP, TOP + 0.09, WOOD_L, 0.015, 2)
b.boxr(-1.5, -1.16, -0.95, 0.32, TOP, TOP + 0.012, RUBBER)
b.finish("ENV_GlassRiser", C_BAR)
order = ['Highball', 'Rocks', 'Martini', 'Coupe', 'Wine', 'Mug', 'Shot']
for i, kind in enumerate(order):
    y = 0.22 - i * 0.18
    make_glass(kind, "PROP_Glass_%s_01" % kind, (-1.3, y, TOP + 0.012))
    make_glass(kind, "PROP_Glass_%s_02" % kind, (-1.64, y, TOP + 0.09))
    marker("MK_GlassSpawn_%s" % kind, (-1.3, y, TOP + 0.012), size=0.05)
make_glass('Shot', "PROP_Glass_Shot_03", (-1.3, 0.22 - 6 * 0.18 + 0.07, TOP + 0.012))

# ticket rail (Papa's style orders) above the left wing
b = MB()
b.box((0.04, 1.0, 0.035), (-1.42, -0.25, 1.78), GOLD, 0.01, 1)
for y in (-0.7, 0.2):
    b.cyl(0.008, H - 1.78, (-1.42, y, 1.78), GOLD, 8)
b.finish("ENV_TicketRail", C_BAR)
for i, y in enumerate((0.05, -0.2, -0.45)):
    t = MB().box((0.004, 0.1, 0.16), (0, 0, -0.08), PAPER, 0.001, 1)
    for k in range(4):
        t.box((0.002, 0.07 - (k % 2) * 0.025, 0.008), (0.003, 0, -0.03 - k * 0.025), [PINK, BLUE, LIME, ORANGE][(i + k) % 4])
    t.finish("PROP_OrderTicket_%02d" % (i + 1), C_PROP, (-1.39, y, 1.77), (0, 0.12, 0))
marker("MK_TicketSlot_01", (-1.39, 0.05, 1.77)); marker("MK_TicketSlot_02", (-1.39, -0.2, 1.77)); marker("MK_TicketSlot_03", (-1.39, -0.45, 1.77))

# right wing: bottle well + tap tower
BOTTLES = [  # name, glass colour, label colour, style
    ("Gin", "Teal", TEAL, 'square'), ("Rum", "Amber", CREAM, 'round'), ("Vodka", "Clear2", BLUE, 'tall'),
    ("Tequila", "Gold", RED, 'flask'), ("BlueCuracao", "Blue", YELLOW, 'round'), ("Grenadine", "Red", PINK, 'tall'),
    ("TripleSec", "Orange", CREAM, 'square'), ("MelonLiqueur", "Green", LIME, 'flask'), ("Violet", "Purple", CREAM, 'round'),
    ("PeachSyrup", "Pink", WHITE, 'tall')]
def bottle_mb(style, gmat, lmat, h=0.3, r=0.042, spout=True):
    segs, rot = 12, (0, 0, 0)
    if style == 'square': segs, rot = 4, (0, 0, PI / 4); r *= 1.25
    if style == 'flask': segs, rot = 6, (0, 0, 0); r *= 1.1
    if style == 'tall': h *= 1.12; r *= 0.88
    prof = [(0, 0), (r * .93, 0), (r, .012), (r, h * .6), (r * .8, h * .7), (r * .34, h * .78), (r * .3, h * .95), (0, h * .95)]
    b = MB().lathe(prof, (0, 0, 0), gmat, segs, rot=rot)
    lr = r * (1.02 if segs > 6 else 1.0)
    b.lathe([(0, h * .22), (lr, h * .22), (lr * 1.02, h * .3), (lr * 1.02, h * .45), (lr, h * .52), (0, h * .52)], (0, 0, 0), lmat, segs, rot=rot)
    if spout:
        b.cyl(r * .36, h * .05, (0, 0, h * .93), STEEL, 10)
        b.cyl(0.006, 0.07, (0, 0, h * .97), STEEL, 8, r2=0.004, rot=(0.35, 0, 0))
    else:
        b.cyl(r * .36, h * .08, (0, 0, h * .93), GOLD, 10)
    return b
b = MB()
b.boxr(1.5, 1.78, -0.95, -0.05, TOP, TOP + 0.1, WOOD_L, 0.015, 2)
b.boxr(1.16, 1.5, -0.95, -0.05, TOP, TOP + 0.012, RUBBER)
b.finish("ENV_BottleRiser", C_BAR)
for i, (nm, gc, lc, st) in enumerate(BOTTLES):
    row_front = i < 5
    x = 1.3 if row_front else 1.64
    y = -0.15 - (i % 5) * 0.18
    z = TOP + (0.012 if row_front else 0.1)
    bottle_mb(st, GL_COL[gc], lc).finish("PROP_Bottle_%s" % nm, C_PROP, (x, y, z), (0, 0, random.uniform(-0.3, 0.3)))
    marker("MK_BottleHome_%s" % nm, (x, y, z), size=0.05)

# tap tower (3 taps: beer, soda, sparkling) with drip tray
b = MB()
b.boxr(1.2, 1.7, 0.03, 0.33, TOP, TOP + 0.03, STEEL, 0.01, 1)
b.boxr(1.23, 1.67, 0.06, 0.3, TOP + 0.03, TOP + 0.035, RUBBER)
b.cyl(0.05, 0.42, (1.6, 0.18, TOP), GOLD, 16)
b.lathe([(0, 0), (0.05, 0), (0.07, 0.03), (0.07, 0.06), (0, 0.06)], (1.6, 0.18, TOP + 0.42), GOLD, 16)
b.box((0.06, 0.3, 0.06), (1.57, 0.18, TOP + 0.37), GOLD, 0.02, 2)
for k, y in enumerate((0.08, 0.18, 0.28)):
    b.cyl(0.012, 0.08, (1.53, y, TOP + 0.37), STEEL, 8, rot=(0, -PI / 2, 0))
    b.cyl(0.01, 0.05, (1.46, y, TOP + 0.37), STEEL, 8, rot=(PI, 0, 0))
b.finish("ENV_TapTower", C_BAR)
for k, (y, m) in enumerate(((0.08, YELLOW), (0.18, PINK), (0.28, TEAL))):
    MB().cyl(0.012, 0.03, (0, 0, 0), STEEL, 8).cyl(0.018, 0.12, (0, 0, 0.03), m, 10, r2=0.024, bevel=0.006) \
        .finish("PROP_TapHandle_%d" % (k + 1), C_PROP, (1.52, y, TOP + 0.4))
    marker("MK_TapPour_%d" % (k + 1), (1.46, y, TOP + 0.04), size=0.05)

# front counter (player side): ice well, mixing station, garnish tray, straws, bell, serve spot
b = MB()   # ice well
b.boxr(-0.75, -0.25, 0.48, 0.82, TOP, TOP + 0.07, STEEL, 0.01, 1)
b.boxr(-0.72, -0.28, 0.51, 0.79, TOP + 0.02, TOP + 0.075, M("M_Metal_SteelDark", "#8f98a8", 0.3, 0.9))
b.finish("ENV_IceWell", C_BAR)
b = MB()
for i in range(26):
    s = random.uniform(0.022, 0.03)
    b.box((s, s, s), (random.uniform(-0.2, 0.2), random.uniform(-0.12, 0.12), random.uniform(0.07, 0.11)), ICE, 0.006, 1,
          rot=(random.random(), random.random(), random.random()))
b.finish("DECO_IceCubes", C_DECO, (-0.5, 0.65, TOP))
MB().lathe([(0, 0), (0.035, 0), (0.04, 0.09), (0.036, 0.09), (0.031, 0.006), (0, 0.006)], (0, 0, 0), STEEL, 16) \
    .cyl(0.012, 0.1, (0, 0, 0), WOOD_D, 8, rot=(PI, 0, 0)) \
    .finish("PROP_IceScoop", C_PROP, (-0.92, 0.6, TOP + 0.045), (PI / 2, 0, 0.3))
marker("MK_IceScoopHome", (-0.92, 0.6, TOP + 0.05))

MB().boxr(-0.15, 0.6, 0.45, 0.78, TOP, TOP + 0.012, RUBBER).finish("ENV_BarMat", C_BAR)
T = TOP + 0.012
# shaker (tin + cap), jigger, bar spoon, strainer, muddler
MB().lathe(SHAKER_TIN_PROFILE, (0, 0, 0), STEEL, 20) \
    .finish("PROP_Shaker_Tin", C_PROP, (0.0, 0.62, T))
MB().lathe([(0, 0), (0.049, 0), (0.051, 0.03), (0.03, 0.07), (0.016, 0.1), (0.018, 0.12), (0, 0.12)], (0, 0, 0), STEEL, 20) \
    .finish("PROP_Shaker_Cap", C_PROP, (0.12, 0.66, T))
MB().lathe([(0, 0), (0.022, 0), (0.024, 0.045), (0.012, 0.05), (0.024, 0.055), (0.026, 0.1), (0.022, 0.1), (0.008, 0.055), (0, 0.055)],
           (0, 0, 0), STEEL, 16).finish("PROP_Jigger", C_PROP, (0.22, 0.6, T))
MB().cyl(0.0045, 0.32, (0, 0, 0), STEEL, 6).sphere(0.012, (0, 0, -0.005), STEEL, 10, 6, (1.2, 0.8, 0.35)) \
    .sphere(0.008, (0, 0, 0.32), GOLD, 8, 6).finish("PROP_BarSpoon", C_PROP, (0.38, 0.7, T + 0.012), (0, PI / 2, -0.4))
MB().cyl(0.045, 0.008, (0, 0, 0), STEEL, 20).ring(0.045, 0.006, 0.006, (0, 0, 0.008), STEEL, 20) \
    .box((0.12, 0.02, 0.006), (0.1, 0, 0.004), STEEL, 0.003, 1).finish("PROP_Strainer", C_PROP, (0.42, 0.55, T))
MB().cyl(0.018, 0.2, (0, 0, 0), WOOD_L, 12).cyl(0.022, 0.03, (0, 0, -0.02), WOOD_D, 12) \
    .finish("PROP_Muddler", C_PROP, (0.5, 0.72, T + 0.022), (0, PI / 2, 0.2))
for nm, p in (("Shaker", (0.0, 0.62, T)), ("Jigger", (0.22, 0.6, T)), ("Strainer", (0.42, 0.55, T))):
    marker("MK_ToolHome_" + nm, p, size=0.05)

# garnish tray: lemon, lime, orange, cherry, mint, blueberry
b = MB()
b.boxr(0.7, 1.08, 0.46, 0.84, TOP, TOP + 0.015, WOOD_L, 0.008, 1)
for x in (0.7, 0.826, 0.953, 1.08):
    b.boxr(x - 0.008, x + 0.008, 0.46, 0.84, TOP, TOP + 0.05, WOOD_L, 0.004, 1)
for y in (0.46, 0.65, 0.84):
    b.boxr(0.7, 1.08, y - 0.008, y + 0.008, TOP, TOP + 0.05, WOOD_L, 0.004, 1)
b.finish("ENV_GarnishTray", C_BAR)
CIT = {"Lemon": YELLOW, "Lime": LIME, "Orange": ORANGE}
cells = [(0.763, 0.555), (0.89, 0.555), (1.016, 0.555), (0.763, 0.745), (0.89, 0.745), (1.016, 0.745)]
for (cx, cy), kind in zip(cells, ["Lemon", "Lime", "Orange", "Cherry", "Mint", "Blueberry"]):
    for n in range(3):
        p = (cx + random.uniform(-0.025, 0.025), cy + (n - 1) * 0.05, TOP + 0.02 + n * 0.004)
        if kind in CIT:
            o = MB().cyl(0.026, 0.008, (0, 0, -0.004), CIT[kind], 16).cyl(0.021, 0.0085, (0, 0, -0.004), WHITE, 16) \
                .finish("PROP_Garnish_%s_%02d" % (kind, n + 1), C_PROP, p, (PI / 2 - 0.4, 0, random.uniform(0, 3)))
        elif kind == "Cherry":
            MB().sphere(0.016, (0, 0, 0.016), RED, 12, 8).cyl(0.0015, 0.04, (0, 0, 0.03), LEAF_D, 5, rot=(0.3, 0, 0)) \
                .finish("PROP_Garnish_Cherry_%02d" % (n + 1), C_PROP, p)
        elif kind == "Mint":
            b = MB().cyl(0.002, 0.05, (0, 0, 0), LEAF_D, 5)
            for k in range(4):
                b.sphere(0.016, (0.012 * math.cos(k * 1.6), 0.012 * math.sin(k * 1.6), 0.03 + k * 0.006), LEAF, 10, 6, (1, 0.55, 0.15), (0.4, 0, k * 1.6))
            b.finish("PROP_Garnish_Mint_%02d" % (n + 1), C_PROP, p, (PI / 2, 0, random.uniform(0, 3)))
        else:
            b = MB()
            for k in range(5):
                b.sphere(0.01, (random.uniform(-0.02, 0.02), random.uniform(-0.015, 0.015), 0.01), BLUE, 10, 6)
            b.finish("PROP_Garnish_Blueberry_%02d" % (n + 1), C_PROP, p)
    marker("MK_GarnishSpawn_" + kind, (cx, cy, TOP + 0.03), size=0.04)

# straw cup + service bell + tip jar
b = MB().lathe([(0, 0), (0.035, 0), (0.038, 0.11), (0.034, 0.11), (0.032, 0.006), (0, 0.006)], (0, 0, 0), TEAL, 16)
for k in range(9):
    a = k * 2.4
    b.cyl(0.0035, 0.2, (0.016 * math.cos(a), 0.016 * math.sin(a), 0.01), [PINK, LIME, YELLOW, BLUE][k % 4], 6,
          rot=(0.12 * math.sin(a), 0.12 * math.cos(a), 0))
b.finish("DECO_StrawCup", C_DECO, (1.25, 0.55, TOP))
MB().cyl(0.05, 0.012, (0, 0, 0), WOOD_D, 16).lathe([(0, 0), (0.04, 0), (0.035, 0.03), (0.018, 0.05), (0, 0.055)], (0, 0, 0.012), GOLD, 16) \
    .cyl(0.006, 0.012, (0, 0, 0.065), GOLD, 8).finish("PROP_ServiceBell", C_PROP, (1.45, 0.75, TOP))
b = MB().lathe([(0, 0), (0.05, 0), (0.055, 0.12), (0.045, 0.14), (0.042, 0.14), (0.05, 0.12), (0.046, 0.006), (0, 0.006)], (0, 0, 0), GLASS, 18)
for k in range(10):
    b.cyl(0.012, 0.003, (random.uniform(-0.025, 0.025), random.uniform(-0.025, 0.025), 0.008 + k * 0.004), GOLD, 10,
          rot=(random.uniform(-0.4, 0.4), random.uniform(-0.4, 0.4), 0))
b.finish("DECO_TipJar", C_DECO, (-1.45, 0.75, TOP))

# serve spot: gold coaster ring at the counter edge where finished drinks go
MB().cyl(0.075, 0.006, (0, 0, 0), NAVY, 32).ring(0.075, 0.008, 0.005, (0, 0, 0.006), E_SERVE, 32) \
    .finish("DECO_ServeCoaster", C_DECO, (0, 0.93, TOP))
marker("MK_ServeSpot", (0, 0.93, TOP + 0.006))
for k, x in enumerate((-0.9, 0.9)):
    MB().cyl(0.06, 0.005, (0, 0, 0), NAVY, 24).ring(0.06, 0.006, 0.004, (0, 0, 0.005), GOLD, 24) \
        .finish("DECO_Coaster_Side_%d" % (k + 1), C_DECO, (x, 0.95, TOP))
    marker("MK_ServeSpot_%s" % ("L" if x < 0 else "R"), (x, 0.95, TOP + 0.005))

# back counter: sink, three juice dispensers, blender, under-counter fridge
b = MB()
b.boxr(-1.45, -0.95, -1.36, -1.02, TOPB - 0.01, TOPB + 0.005, STEEL, 0.006, 1)
b.boxr(-1.4, -1.0, -1.31, -1.07, TOPB - 0.012, TOPB + 0.006, M("M_Metal_SteelDark", "#8f98a8", 0.3, 0.9))
b.cyl(0.014, 0.28, (-1.2, -1.33, TOPB), STEEL, 10)
b.cyl(0.012, 0.18, (-1.2, -1.33, TOPB + 0.27), STEEL, 10, rot=(-PI / 2, 0, 0))
b.cyl(0.012, 0.04, (-1.2, -1.16, TOPB + 0.24), STEEL, 10)
for x in (-1.27, -1.13):
    b.cyl(0.015, 0.025, (x, -1.33, TOPB + 0.12), [RED, BLUE][x > -1.2], 10, rot=(-PI / 2, 0, 0))
b.finish("ENV_Sink", C_BAR)
marker("MK_SinkTap", (-1.2, -1.16, TOPB + 0.2))
for k, (x, liq, cap) in enumerate(((-0.55, "Orange", ORANGE), (-0.2, "Pink", PINK), (0.15, "Blue", BLUE))):
    b = MB()
    b.boxr(-0.11, 0.11, -0.11, 0.11, 0, 0.1, WOOD_L, 0.015, 2)
    b.lathe([(0, 0), (0.09, 0), (0.1, 0.02), (0.1, 0.26), (0.085, 0.28), (0.082, 0.28), (0.093, 0.26), (0.093, 0.024), (0, 0.024)],
            (0, 0, 0.1), GLASS, 24)
    b.lathe([(0, 0), (0.092, 0), (0.092, 0.2), (0, 0.2)], (0, 0, 0.125), LIQ[liq], 24)
    b.lathe([(0, 0), (0.09, 0), (0.09, 0.03), (0.04, 0.06), (0.02, 0.07), (0, 0.075)], (0, 0, 0.38), cap, 24)
    b.cyl(0.012, 0.06, (0, 0.09, 0.15), STEEL, 8, rot=(-PI / 2, 0, 0))
    b.cyl(0.01, 0.03, (0, 0.14, 0.12), STEEL, 8)
    b.finish("ENV_Dispenser_%s" % liq, C_BAR, (x, -1.2, TOPB))
    MB().cyl(0.01, 0.01, (0, 0, 0), STEEL, 8).box((0.012, 0.02, 0.05), (0, 0, 0.035), cap, 0.005, 1) \
        .finish("PROP_DispenserLever_%s" % liq, C_PROP, (x, -1.06, TOPB + 0.27))
    marker("MK_DispenserPour_%s" % liq, (x, -1.06, TOPB + 0.005), size=0.05)
b = MB()   # blender machine
b.lathe([(0, 0), (0.08, 0), (0.085, 0.02), (0.075, 0.13), (0, 0.13)], (0, 0, 0), WHITE, 6, rot=(0, 0, PI / 6))
b.cyl(0.02, 0.012, (0.0, 0.07, 0.07), TEAL, 10, rot=(-PI / 2, 0, 0))
b.cyl(0.015, 0.012, (0.045, 0.06, 0.06), PINK, 10, rot=(-PI / 2, 0, 0.4))
b.finish("ENV_Blender_Base", C_BAR, (0.75, -1.2, TOPB))
MB().lathe(BLENDER_JAR_PROFILE, (0, 0, 0), GLASS, 20) \
    .lathe([(0, 0.24), (0.09, 0.24), (0.09, 0.27), (0.03, 0.28), (0, 0.29)], (0, 0, 0), TEAL, 20) \
    .box((0.03, 0.05, 0.12), (0.095, 0, 0.13), GLASS, 0.01, 1) \
    .finish("PROP_Blender_Jar", C_PROP, (0.75, -1.2, TOPB + 0.13))
marker("MK_BlenderSocket", (0.75, -1.2, TOPB + 0.13))
b = MB()   # fridge under the back counter, glass door facing the player
b.boxr(0.95, 1.75, -1.02, -0.99, 0.08, 0.86, STEEL, 0.015, 2)
b.boxr(1.0, 1.7, -1.03, -0.995, 0.13, 0.81, M("M_Glass_Fridge", "#cfefff", 0.05, 0, 0, 0.35))
b.boxr(1.0, 1.7, -1.38, -1.35, 0.13, 0.81, E_FRIDGE)          # glowing back panel (hollow inside, bottles are physics props)
b.boxr(0.97, 1.0, -1.38, -1.03, 0.1, 0.84, STEEL)               # side walls
b.boxr(1.7, 1.73, -1.38, -1.03, 0.1, 0.84, STEEL)
b.boxr(1.0, 1.7, -1.38, -1.03, 0.1, 0.13, M("M_Emit_FridgeFloor", "#d8f6ff", 1, 0, 1.5))
b.boxr(1.0, 1.7, -1.38, -1.03, 0.81, 0.84, STEEL)
b.boxr(1.0, 1.7, -1.36, -1.04, 0.45, 0.47, STEEL)
b.box((0.03, 0.02, 0.3), (1.03, -1.04, 0.5), GOLD, 0.008, 1)
b.finish("ENV_Fridge", C_BAR)
for row, z in enumerate((0.13, 0.47)):
    for k in range(5):
        x = 1.08 + k * 0.14
        nm = ["Soda", "Tonic", "Juice", "Milk", "Cola"][k]
        col = [GL_COL["Green"], GL_COL["Clear2"], GL_COL["Orange"], WHITE, GL_COL["Red"]][k]
        bottle_mb('round', col, [LIME, BLUE, ORANGE, BLUE, RED][k], 0.22, 0.03, spout=False) \
            .finish("PROP_Fridge_%s_%d" % (nm, row + 1), C_PROP, (x, -1.2, z))

# ================================================================ CUSTOMER SIDE
# customer spots: centre = standing "ordering" spot, left / right = optional stools
for nm, x in (("C", 0.0), ("L", -1.0), ("R", 1.0)):
    marker("MK_CustomerSpot_" + nm, (x, 1.42, PLAT), (0, 0, PI))
MB().cyl(0.38, 0.012, (0, 0, 0), PINK, 32).ring(0.32, 0.015, 0.008, (0, 0, 0.012), CREAM, 32) \
    .star(0.12, (0, 0, 0.012), YELLOW, 0.004).finish("DECO_OrderRug", C_DECO, (0, 1.45, PLAT))
for k, (x, cush) in enumerate(((-1.0, TEAL), (1.0, YELLOW))):
    b = MB()
    b.cyl(0.17, 0.03, (0, 0, 0), GOLD, 20, bevel=0.01)
    b.cyl(0.035, 0.36, (0, 0, 0.03), GOLD, 12)
    b.ring(0.14, 0.012, 0.012, (0, 0, 0.17), GOLD, 20)
    b.cyl(0.2, 0.06, (0, 0, 0.36), WOOD, 24, bevel=0.02)
    b.cyl(0.19, 0.06, (0, 0, 0.41), cush, 24, r2=0.17, bevel=0.025)
    b.finish("ENV_Stool_%s" % ("L" if x < 0 else "R"), C_STOOL, (x, 1.48, PLAT))
    marker("MK_StoolSeat_%s" % ("L" if x < 0 else "R"), (x, 1.48, PLAT + 0.47), (0, 0, PI))
for nm, p in (("Spawn_L", (-RX - 0.7, 2.7, PLAT)), ("Spawn_R", (RX + 0.7, 2.7, PLAT)),
              ("Queue_L1", (-2.0, 2.3, PLAT)), ("Queue_L2", (-3.0, 2.5, PLAT)),
              ("Queue_R1", (2.0, 2.3, PLAT)), ("Queue_R2", (3.0, 2.5, PLAT))):
    marker("MK_" + nm, p, size=0.25)
marker("MK_PlayerStart", (0, -0.25, 0), size=0.4)

# lounge: two tables with cute chairs sized for 1 m characters
def chair(name, loc, rot, cush):
    b = MB()
    for dx in (-0.13, 0.13):
        for dy in (-0.13, 0.13):
            b.cyl(0.025, 0.28, (dx, dy, 0), WOOD_D, 8, r2=0.02)
    b.box((0.36, 0.36, 0.06), (0, 0, 0.3), WOOD, 0.025, 2)
    b.box((0.32, 0.32, 0.05), (0, 0, 0.35), cush, 0.022, 2)
    b.box((0.36, 0.06, 0.34), (0, 0.17, 0.5), WOOD, 0.03, 2)
    b.sphere(0.05, (0, 0.17, 0.69), cush, 12, 8, (1.6, 0.6, 0.6))
    return b.finish(name, C_DECO, loc, rot)
for side, tx in (("L", -2.9), ("R", 2.9)):
    MB().cyl(1.05, 0.012, (0, 0, 0), NAVY, 40).ring(0.9, 0.04, 0.007, (0, 0, 0.012), GOLD, 40) \
        .cyl(0.6, 0.014, (0, 0, 0), TEAL_D, 40).finish("DECO_Rug_%s" % side, C_DECO, (tx, 3.5, PLAT))
    b = MB()
    b.cyl(0.22, 0.04, (0, 0, 0), WOOD_D, 20, bevel=0.015)
    b.cyl(0.05, 0.48, (0, 0, 0.04), GOLD, 12)
    b.cyl(0.42, 0.05, (0, 0, 0.5), WOOD, 32, bevel=0.02)
    b.cyl(0.04, 0.07, (0.1, 0.05, 0.55), GLASS, 12)
    b.cyl(0.02, 0.04, (0.1, 0.05, 0.56), E_LANT, 10)
    b.cyl(0.025, 0.1, (-0.12, -0.06, 0.55), PINK, 10, r2=0.015)
    for k in range(3):
        b.sphere(0.03, (-0.12 + 0.02 * math.cos(k * 2.1), -0.06 + 0.02 * math.sin(k * 2.1), 0.67), [YELLOW, WHITE, PINK][k], 10, 6)
    b.finish("DECO_Table_%s" % side, C_DECO, (tx, 3.5, PLAT))
    chair("DECO_Chair_%s1" % side, (tx - 0.62, 3.5, PLAT), (0, 0, PI / 2), [TEAL, PINK][side == "R"])
    chair("DECO_Chair_%s2" % side, (tx + 0.62, 3.5, PLAT), (0, 0, -PI / 2), [TEAL, PINK][side == "R"])
    marker("MK_ChairSeat_%s1" % side, (tx - 0.62, 3.5, PLAT + 0.375), size=0.1)
    marker("MK_ChairSeat_%s2" % side, (tx + 0.62, 3.5, PLAT + 0.375), size=0.1)
    marker("MK_Table_%s" % side, (tx, 3.5, PLAT + 0.55), size=0.1)
    # paper lantern above each table
    b = MB().cyl(0.006, 0.55, (0, 0, 0.32), WOOD_D, 6)
    b.sphere(0.24, (0, 0, 0), E_LANT, 12, 8, (1, 1, 1.15))
    for z in (-0.12, 0, 0.12):
        b.ring(0.235 * math.cos(math.asin(z / 0.28)), 0.006, 0.006, (0, 0, z), WOOD_D, 24)
    b.cyl(0.08, 0.04, (0, 0, 0.26), WOOD_D, 12).cyl(0.08, 0.04, (0, 0, -0.3), WOOD_D, 12)
    b.finish("DECO_Lantern_%s" % side, C_DECO, (tx, 3.5, H - 0.87))
    light("LGT_Lantern_%s" % side, 'POINT', (tx, 3.5, H - 0.87), 60, "#ffb46b", 0.24)

# jukebox (front-left corner)
b = MB()
b.box((0.7, 0.45, 1.0), (0, 0, 0.5), WOOD, 0.04, 2)
b.lathe([(0, -0.225), (0.35, -0.225), (0.35, 0.225), (0, 0.225)], (0, 0, 1.0), WOOD, 24, rot=(PI / 2, 0, 0), arc=(0, PI))
for k, (rr, m) in enumerate(((0.3, E_PINK), (0.26, E_YEL), (0.22, E_TEAL))):
    b.ring(rr, 0.012, 0.012, (0, 0, 1.0), m, 24, rot=(PI / 2, 0, 0), arc=(0, PI))
    b.ring(rr, 0.012, 0.012, (0, 0, 1.0), m, 24, rot=(PI / 2, 0, 0), arc=(0, PI))
b.box((0.5, 0.05, 0.3), (0, -0.21, 0.78), CREAM, 0.02, 2)
b.box((0.5, 0.05, 0.35), (0, -0.21, 0.3), NAVY, 0.02, 2)
for k in range(5):
    b.box((0.42, 0.03, 0.02), (0, -0.24, 0.17 + k * 0.06), GOLD, 0.005, 1)
o = b.finish("DECO_Jukebox", C_DECO, (-3.75, YF - 0.3, PLAT))
# rings were built in local XZ plane facing -Y, nudge them forward
light("LGT_Jukebox", 'POINT', (-3.75, YF - 0.8, PLAT + 1.1), 20, "#ff8fc0", 0.2)

# plants (Animal-Crossing style blobby leaves)
def plant(name, loc, s=1.0, tall=False):
    b = MB()
    b.lathe([(0, 0), (0.15, 0), (0.19, 0.3), (0.21, 0.32), (0.21, 0.36), (0, 0.36)], (0, 0, 0), POT, 16, scale=s)
    b.cyl(0.17 * s, 0.01, (0, 0, 0.35 * s), WOOD_D, 16)
    n = 9 if tall else 6
    for k in range(n):
        a = k * 2.4
        h = (0.45 + 0.75 * (k / n) if tall else 0.42 + 0.1 * (k % 3)) * s
        r = (0.06 + 0.18 * (1 - k / n)) * s if tall else 0.14 * s
        b.sphere((0.17 if tall else 0.13) * s, (r * math.cos(a), r * math.sin(a), h), [LEAF, LEAF_D][k % 2], 8, 5,
                 (1, 0.65, 0.45), (0.5, 0, a))
    if tall:
        b.cyl(0.02 * s, 1.0 * s, (0, 0, 0.35 * s), WOOD_D, 6)
    return b.finish(name, C_DECO, loc)
plant("DECO_Plant_FrontR", (3.95, YF - 0.45, PLAT), 1.25, True)
plant("DECO_Plant_SideL", (-RX + 0.45, 0.2, PLAT), 1.1, True)
plant("DECO_Plant_SideR", (RX - 0.45, 0.2, PLAT), 1.1, True)
plant("DECO_Plant_SillL", (-2.6, YF - 0.05, 2.3 - 0.55 - 0.03), 0.45)
plant("DECO_Plant_SillR", (2.6, YF - 0.05, 2.3 - 0.55 - 0.03), 0.45)
plant("DECO_Plant_CounterL", (-1.6, 0.85, TOP), 0.38)

# pendant lamps over the bar
for k, x in enumerate((-1.0, 0.0, 1.0)):
    b = MB()
    b.cyl(0.006, H - 2.72, (0, 0, 2.72), WOOD_D, 6)
    b.lathe([(0.03, 0.2), (0.05, 0.18), (0.16, 0.04), (0.17, 0.0), (0.155, 0.0), (0.145, 0.04), (0.04, 0.17), (0, 0.18)],
            (0, 0, 2.52), [TEAL, GOLD, PINK][k] if k != 1 else GOLD, 24)
    b.sphere(0.05, (0, 0, 2.55), E_WARM, 12, 8)
    b.finish("DECO_Pendant_%d" % (k + 1), C_DECO, (x, 0.75, 0))
    light("LGT_Pendant_%d" % (k + 1), 'SPOT', (x, 0.75, 2.52), 60, "#ffd49a", 0.05, (x, 0.75, 0))
    bpy.data.objects["LGT_Pendant_%d" % (k + 1)].data.spot_size = math.radians(110)
    bpy.data.objects["LGT_Pendant_%d" % (k + 1)].data.spot_blend = 0.6

# string lights across the front wall and down the side walls
def string_lights(name, p0, p1, sag, n):
    b = MB(); cols = [E_WARM, E_PINK, E_TEAL, E_YEL]
    p0, p1 = Vector(p0), Vector(p1)
    pts = [p0.lerp(p1, t / n) - Vector((0, 0, sag * 4 * (t / n) * (1 - t / n))) for t in range(n + 1)]
    for i in range(n):
        d = pts[i + 1] - pts[i]
        q = d.to_track_quat('Z', 'Y').to_euler()
        b.cyl(0.004, d.length, tuple(pts[i]), NAVY, 3, rot=tuple(q))
        b.sphere(0.028, tuple(pts[i] - Vector((0, 0, 0.03))), cols[i % 4], 8, 4, (1, 1, 1.3))
    return b.finish(name, C_DECO)
string_lights("DECO_StringLights_Front", (-RX + 0.05, YF - 0.08, 3.3), (RX - 0.05, YF - 0.08, 3.3), 0.4, 30)
string_lights("DECO_StringLights_Left", (-RX + 0.08, YB + 0.2, 3.25), (-RX + 0.08, YF - 0.2, 3.25), 0.3, 18)
string_lights("DECO_StringLights_Right", (RX - 0.08, YB + 0.2, 3.25), (RX - 0.08, YF - 0.2, 3.25), 0.3, 18)

# front wall neon: cocktail glass + crescent moon flanking the moon window
neon_poly("DECO_Neon_Cocktail", [(-0.22, 0.2), (0.22, 0.2), (0.015, -0.03), (0.015, -0.22), (0.12, -0.27), (-0.12, -0.27),
          (-0.015, -0.22), (-0.015, -0.03)], True, (-1.48, YF - 0.06, 2.45), FACE_PLAYER, E_TEAL, 0.013)
cres = [(0.25 * math.cos(a), 0.25 * math.sin(a)) for a in [math.radians(60 + i * 12) for i in range(21)]]
cres += [(0.07 + 0.19 * math.cos(a), 0.03 + 0.19 * math.sin(a)) for a in [math.radians(285 - i * 11) for i in range(19)]]
neon_poly("DECO_Neon_Moon", cres, True, (1.48, YF - 0.06, 2.45), FACE_PLAYER, E_YEL, 0.013)
light("LGT_Neon_FrontL", 'POINT', (-1.48, YF - 0.4, 2.45), 12, "#4ff0e6", 0.2)
light("LGT_Neon_FrontR", 'POINT', (1.48, YF - 0.4, 2.45), 12, "#ffe066", 0.2)

# star decals on the upper walls + framed pictures
b = MB()
for i in range(26):
    wall = random.choice(['L', 'R', 'F'])
    z = random.uniform(1.5, 3.2)
    if wall == 'F':
        x = random.choice([random.uniform(-4.3, -3.3), random.uniform(3.3, 4.3), random.uniform(-1.9, -1.1), random.uniform(1.1, 1.9)])
        if 1.1 < abs(x) < 1.9 and 2.0 < z < 2.85: continue
        b.star(random.uniform(0.04, 0.08), (x, YF - 0.005, z), GOLD, 0.006, FACE_PLAYER)
    else:
        sx = -1 if wall == 'L' else 1
        y = random.uniform(-1.0, 4.6)
        if 1.9 < y < 3.5: continue
        b.star(random.uniform(0.04, 0.08), (sx * (RX - 0.005), y, z), GOLD, 0.006, (0, -sx * PI / 2, 0))
b.finish("DECO_WallStars", C_DECO)
for side, sx in (("L", -1), ("R", 1)):
    for k, (y, col) in enumerate(((0.9, [PINK, BLUE]), (4.15, [LIME, ORANGE]))):
        b = MB()
        b.box((0.04, 0.5, 0.62), (0, 0, 0), GOLD, 0.015, 2)
        b.box((0.05, 0.42, 0.54), (0, 0, 0), CREAM)
        b.sphere(0.1, (-sx * 0.03, 0, 0.05), col[0], 12, 8, (0.2, 1, 1))
        b.box((0.012, 0.3, 0.12), (-sx * 0.03, 0, -0.15), col[1], 0.01, 1)
        b.finish("DECO_Picture_%s%d" % (side, k + 1), C_DECO, (sx * (RX - 0.03), y, 2.0))
    b = MB()   # wall sconces beside the doors
    for y in (1.95, 3.45):
        b.box((0.05, 0.12, 0.18), (0, y, 1.85), GOLD, 0.02, 2)
        b.sphere(0.07, (-sx * 0.09, y, 1.98), E_WARM, 12, 8)
    b.finish("DECO_Sconces_%s" % side, C_DECO, (sx * (RX - 0.02), 0, 0))
    for y in (1.95, 3.45):
        light("LGT_Sconce_%s_%d" % (side, int(y)), 'POINT', (sx * (RX - 0.2), y, 1.98), 15, "#ffd08a", 0.08)

# chalkboard menu on the left of the back wall (customer side, visible to the player when turning)
b = MB()
b.box((1.1, 0.04, 0.75), (0, 0, 0), WOOD, 0.02, 2)
b.box((0.98, 0.05, 0.63), (0, 0, 0), M("M_Chalkboard", "#26313a", 0.95))
for k in range(5):
    b.box((0.35 + 0.25 * (k % 2), 0.055, 0.025), (-0.12, 0, 0.2 - k * 0.1), [CREAM, PINK, CREAM, LIME, CREAM][k], 0.005, 1)
    b.box((0.08, 0.055, 0.025), (0.36, 0, 0.2 - k * 0.1), YELLOW, 0.005, 1)
b.finish("DECO_MenuBoard", C_DECO, (-3.2, YB + 0.03, 2.1))
plant("DECO_Plant_BackR", (3.2, YB + 0.3, PLAT), 0.9, True)

# ================================================================ NIGHT SKY OUTSIDE THE WINDOWS
SY = YF + 4.0
MB().box((30, 0.05, 16), (0, SY + 0.5, 4), E_SKY).finish("SKY_Backdrop", C_SKY)
MB().box((30, 0.05, 2.2), (0, SY + 0.4, 1.4), E_SKY2).box((30, 0.05, 1.0), (0, SY + 0.35, 0.6), E_SKY3).finish("SKY_HorizonGlow", C_SKY)
b = MB()
for (hx, hz, sxh, szh) in ((-4.5, -0.4, 5.0, 1.8), (2.5, -0.5, 6.0, 1.7), (7.0, -0.2, 3.5, 1.6), (-0.8, -0.8, 4.0, 1.6)):
    b.sphere(1.0, (hx, SY - 0.6 + random.uniform(-0.2, 0.2), hz), E_HILL, 24, 12, (sxh, 0.3, szh))
b.finish("SKY_Hills", C_SKY)
b = MB()
for (hx, hz, s) in ((-3.0, 1.05, 1.0), (-2.4, 1.0, 0.8), (1.5, 1.0, 0.9), (2.2, 0.95, 0.7), (3.4, 0.85, 1.0), (-5.0, 1.1, 0.8)):
    b.box((0.4 * s, 0.3, 0.35 * s), (hx, SY - 1.2, hz), E_HILL)
    b.lathe([(0, 0), (0.32 * s, 0), (0, 0.22 * s)], (hx, SY - 1.2, hz + 0.175 * s), E_HILL, 4, rot=(0, 0, PI / 4))
    b.box((0.08 * s, 0.02, 0.08 * s), (hx - 0.08 * s, SY - 1.36, hz), E_WIN)
    b.box((0.08 * s, 0.02, 0.08 * s), (hx + 0.08 * s, SY - 1.36, hz + 0.03), E_WIN)
b.finish("SKY_Village", C_SKY)
b = MB()
for i in range(50):
    b.star(random.uniform(0.03, 0.09), (random.uniform(-9, 9), SY + 0.3, random.uniform(1.9, 8)), E_STAR, 0.01, FACE_PLAYER, 4, 0.35)
b.finish("SKY_Stars", C_SKY)
b = MB().cyl(0.6, 0.05, (0, 0, 0), E_MOON, 40)
for (cx, cy, cr) in ((0.18, 0.15, 0.12), (-0.2, -0.1, 0.08), (0.05, -0.28, 0.06)):
    b.cyl(cr, 0.02, (cx, cy, -0.01), M("M_Emit_MoonCrater", "#f1dfa8", 1, 0, 3), 16)
b.finish("SKY_Moon", C_SKY, (0.75, SY + 0.2, 3.35), FACE_PLAYER)

# ================================================================ LIGHTS (Blender preview; Unity: bake or use as reference)
light("LGT_BarKey", 'AREA', (0, -0.2, H - 0.25), 80, "#ffe6c2", 2.6, (0, 0.0, 0))
light("LGT_CustomerKey", 'AREA', (0, 0.1, 2.9), 35, "#ffd9c4", 1.2, (0, 1.5, 1.3))
light("LGT_LoungeFill", 'AREA', (0, 3.0, H - 0.3), 160, "#ffcf9a", 4.0, (0, 3.0, 0))
light("LGT_Moonlight", 'AREA', (0, YF + 1.5, 3.0), 300, "#9fb6ff", 2.5, (0, 2.0, 0.5))
w = bpy.data.worlds.new("World"); scene.world = w; w.use_nodes = True
bg = w.node_tree.nodes['Background']; bg.inputs[0].default_value = hexcol("#2a3570"); bg.inputs[1].default_value = 0.35

# ================================================================ STATS + EXPORT
dg = bpy.context.evaluated_depsgraph_get()
tris, objs, per = 0, 0, []
for ob in scene.objects:
    if ob.type == 'MESH' and ob.users_collection[0] not in (C_CUT, C_PREV):
        me = ob.evaluated_get(dg).to_mesh()
        me.calc_loop_triangles(); tris += len(me.loop_triangles); objs += 1
        per.append((len(me.loop_triangles), ob.name))
        ob.evaluated_get(dg).to_mesh_clear()
print("TOP", sorted(per, reverse=True)[:15])
props = len(C_PROP.objects)
stats = "Moonlit Tails bar - triangles: %d  mesh objects: %d  interactable props: %d  materials: %d" % (tris, objs, props, len(MATS))
print(stats)
os.makedirs(OUT, exist_ok=True)
open(os.path.join(OUT, "stats.txt"), "w").write(stats + "\n")
import json
json.dump(MAT_INFO, open(os.path.join(OUT, "MoonlitBar_materials.json"), "w"), indent=1)

bpy.ops.object.select_all(action='DESELECT')
for ob in scene.objects:
    if ob.users_collection[0] not in (C_CUT, C_PREV) and ob.type in ('MESH', 'EMPTY', 'LIGHT'):
        ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "MoonlitBar.fbx"), use_selection=True,
                         object_types={'MESH', 'EMPTY', 'LIGHT'}, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True, use_mesh_modifiers=True,
                         mesh_smooth_type='OFF', add_leaf_bones=False, use_custom_props=False)
C_CUT.hide_viewport = True
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "MoonlitBar.blend"), compress=True)

# ================================================================ PREVIEW: drop Kintsu in for scale + renders
if KINTSU and os.path.exists(KINTSU):
    try:
        with bpy.data.libraries.load(KINTSU, link=False) as (src, dst):
            dst.collections = [c for c in src.collections if not c.startswith("_")]
        root = bpy.data.objects.new("Kintsu_Root", None); C_PREV.objects.link(root)
        names = set(c.name for c in dst.collections if c)
        for c in dst.collections:
            if c is None: continue
            for ob in c.all_objects:
                if ob.type in ('CAMERA', 'LIGHT'): continue
                if ob.name not in C_PREV.objects: C_PREV.objects.link(ob)
        for ob in C_PREV.objects:
            if ob.parent is None and ob is not root: ob.parent = root
        root.location = (0, 1.42, PLAT)
        bpy.context.view_layer.update()
        zs = [(ob.matrix_world @ Vector(v)).z for ob in C_PREV.objects if ob.type == 'MESH' for v in ob.bound_box]
        print("Kintsu height:", max(zs) - min(zs), "collections:", names)
    except Exception as e:
        print("Kintsu append failed:", e)

if DO_RENDER:
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.eevee.taa_render_samples = 48
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = 0.0
    try:
        scene.use_nodes = True
        nt = scene.node_tree
        rl = nt.nodes.get('Render Layers') or nt.nodes.new('CompositorNodeRLayers')
        comp = nt.nodes.get('Composite') or nt.nodes.new('CompositorNodeComposite')
        gl = nt.nodes.new('CompositorNodeGlare'); gl.glare_type = 'FOG_GLOW'
        try: gl.quality = 'MEDIUM'; gl.threshold = 1.0; gl.size = 8
        except Exception: pass
        nt.links.new(rl.outputs['Image'], gl.inputs['Image']); nt.links.new(gl.outputs['Image'], comp.inputs['Image'])
    except Exception as e:
        print("glare setup failed", e)
    cams = [("POV_Player", (0, -0.3, 1.62), (0, 2.6, 1.3), 17),
            ("Stations", (0, -0.75, 1.85), (0, 0.35, 0.95), 15),
            ("Overview", (4.1, -1.15, 3.25), (-0.8, 2.0, 0.6), 14),
            ("BackBar", (0.6, 3.6, 1.55), (0, -1.0, 1.6), 18)]
    only = argv[3].split(",") if len(argv) > 3 else None
    for nm, p, t, lens in cams:
        if only and nm not in only: continue
        cd = bpy.data.cameras.new(nm); cd.lens = lens; cd.clip_start = 0.02
        co = bpy.data.objects.new("CAM_" + nm, cd); C_PREV.objects.link(co); co.location = p; aim(co, t)
        scene.camera = co
        scene.render.filepath = os.path.join(OUT, "preview_%s.png" % nm)
        bpy.ops.render.render(write_still=True)
        print("rendered", nm)
