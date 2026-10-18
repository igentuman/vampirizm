"""Run with Blender --background --python generate_coffins.py.

Original assets, no downloaded content. Writes editable .blend, runtime mesh JSON,
512px tileable albedo textures, and transparent hammer icons. Unity coordinates
are X/right, Y/up, Z/head; winding is reversed on export for the axis swap.
"""
import bpy
import json
import math
from pathlib import Path
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ASSETS = HERE.parent
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for material in list(bpy.data.materials):
    bpy.data.materials.remove(material)

def texture(name, base, kind):
    n = 512
    y, x = np.mgrid[0:n, 0:n] / n
    rng = np.random.default_rng(81)
    noise = rng.random((n, n))
    if kind == 'wood':
        grain = np.sin(x * 230 + 2.5 * np.sin(y * math.tau) + np.sin(x * 31 + y * math.tau))
        value = .72 + .18 * grain + .14 * noise
        value -= .15 * (np.sin(x * math.tau * 8) > .98)
    elif kind == 'iron':
        value = .65 + .35 * noise + .08 * np.sin(x * 250 + y * 120)
    else:
        value = .8 + .15 * np.sin(x * 500) * np.sin(y * 500) + .1 * noise
    rgba = np.ones((n, n, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(value[:, :, None] * np.array(base), 0, 1)
    im = bpy.data.images.new('tex_coffin_' + name, n, n)
    im.pixels.foreach_set(rgba.ravel())
    im.filepath_raw = str(ASSETS / ('tex_coffin_' + name + '.png'))
    im.file_format = 'PNG'
    im.save()
    im.pack()
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(node for node in mat.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Roughness'].default_value = .52 if kind == 'iron' else .86
    bsdf.inputs['Metallic'].default_value = .8 if kind == 'iron' else 0
    tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
    tex.image = im
    mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    return mat

mats = {
    'wood': texture('wood', (.49, .29, .13), 'wood'),
    'ancient': texture('ancient', (.20, .24, .21), 'wood'),
    'iron': texture('iron', (.39, .43, .46), 'iron'),
    'lining': texture('lining', (.37, .025, .045), 'cloth'),
}
outline = [(-.33, -1.15), (.33, -1.15), (.60, .48), (.42, 1.15), (-.42, 1.15), (-.60, .48)]
hinge = Vector((-.60, 0, .77))
objects = []

def finish(obj, name, material, lid=False, bevel=0):
    obj.name = name
    obj.data.materials.append(mats[material])
    obj['lid'] = lid
    if bevel:
        mod = obj.modifiers.new('Worn edges', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    # Cube projection gives each bevel a real UV rather than relying on shader-only coordinates.
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.cube_project(cube_size=1)
    bpy.ops.object.mode_set(mode='OBJECT')
    objects.append(obj)
    return obj

def box(name, loc, scale, material, lid=False, bevel=.012):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    ob = bpy.context.object
    ob.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(ob, name, material, lid, bevel)

def slab(name, points, z, height, material, lid=False):
    verts = [(x,y,h) for h in (z, z+height) for x,y in points]
    count = len(points)
    faces = [tuple(reversed(range(count))), tuple(range(count, 2*count))]
    faces += [(i, (i+1)%count, (i+1)%count+count, i+count) for i in range(count)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(ob)
    return finish(ob, name, material, lid, .007)

def edge(name, a, b, z, depth, height, material, lid=False):
    a, b = Vector(a), Vector(b)
    ob = box(name, ((a.x+b.x)/2, (a.y+b.y)/2, z), ((b-a).length, depth, height), material, lid)
    ob.rotation_euler.z = math.atan2(b.y-a.y, b.x-a.x)
    return ob

def export(name):
    groups = {}
    deps = bpy.context.evaluated_depsgraph_get()
    for ob in objects:
        mesh = ob.evaluated_get(deps).to_mesh()
        mesh.calc_loop_triangles()
        key = (ob.data.materials[0].name, bool(ob['lid']))
        part = groups.setdefault(key, dict(material=key[0], lid=key[1], vertices=[], normals=[], uv=[], triangles=[]))
        for tri in mesh.loop_triangles:
            start = len(part['vertices']) // 3
            for li in tri.loops:
                loop = mesh.loops[li]
                v = ob.matrix_world @ mesh.vertices[loop.vertex_index].co
                if key[1]: v -= hinge
                normal = ob.matrix_world.to_3x3() @ tri.normal
                part['vertices'].extend(round(float(t), 6) for t in (v.x, v.z, v.y))
                part['normals'].extend(round(float(t), 6) for t in (normal.x, normal.z, normal.y))
                part['uv'].extend(round(float(t), 6) for t in mesh.uv_layers.active.data[li].uv)
            part['triangles'].extend((start, start+2, start+1))
        ob.evaluated_get(deps).to_mesh_clear()
    (HERE / (name+'.json')).write_text(json.dumps({'parts':list(groups.values())}, separators=(',',':')))
    print(name, 'triangles:', sum(len(p['triangles'])//3 for p in groups.values()))

scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 32
scene.render.resolution_x = scene.render.resolution_y = 256
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.world.color = (.32,.32,.32)
bpy.ops.object.camera_add(location=(3.2,-4.2,4.0))
camera = bpy.context.object
camera.rotation_euler = (Vector((0,0,.45))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 3.15
scene.camera = camera
for loc, power, size in [((1,-3,5),650,4), ((-3,1,3),500,3), ((1,4,4),800,3)]:
    bpy.ops.object.light_add(type='AREA', location=loc)
    light = bpy.context.object
    light.data.energy, light.data.shape, light.data.size = power, 'DISK', size
    light.rotation_euler = (-light.location).to_track_quat('-Z','Y').to_euler()

for vampire, name in [(False, 'wooden_coffin'), (True, 'vampire_coffin')]:
    for ob in objects: bpy.data.objects.remove(ob, do_unlink=True)
    objects = []
    wood = 'ancient' if vampire else 'wood'
    slab('bottom', outline, .15, .13, wood)
    for i, a in enumerate(outline):
        b = outline[(i+1)%6]
        for row in range(3):
            edge('side_plank', a, b, .35+row*.155, .075, .149, wood)
        edge('rim', a, b, .76, .11, .055, 'iron' if vampire else wood)
        if vampire:
            edge('lower_iron_band', a, b, .29, .095, .065, 'iron')
    for y in (-.8,.7):
        box('foot', (0,y,.09), (.73,.15,.18), wood)
    inner = [(x*.84,y*.94) for x,y in outline]
    slab('sleeping_surface', inner, .28, .065, 'lining' if vampire else wood)
    if vampire:
        box('pillow', (0,.78,.39), (.60,.33,.14), 'lining', bevel=.05)
        # Alternating oval links hang along both long sides, with real open holes.
        for side in (-1,1):
            for i in range(17):
                t = i/16
                y = -.78 + t*1.34
                x = side*(.40+.19*t+.055)
                z = .57-.20*math.sin(t*math.pi)
                bpy.ops.mesh.primitive_torus_add(major_segments=12, minor_segments=4, location=(x,y,z), major_radius=.058, minor_radius=.013)
                ob=bpy.context.object
                ob.rotation_euler = (math.pi/2, (i%2)*math.pi/2, 0)
                ob.scale = (1,1.35,1)
                finish(ob,'chain_link','iron')
            for y in (-.78,.56):
                box('iron_bracket',(side*(.43 if y<0 else .61),y,.52),(.06,.09,.43),'iron')
        slab('lid', [(x*1.035,y*1.025) for x,y in outline], .79, .075, wood, True)
        for y in (-.15,.65):
            box('hinge_body_leaf',(-.56,y,.65),(.12,.12,.25),'iron')
            box('hinge_lid_leaf',(-.49,y,.88),(.30,.12,.04),'iron',True)
            bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=.04, depth=.18, location=(-.60,y,.79))
            ob=bpy.context.object
            ob.rotation_euler.x=math.pi/2
            finish(ob,'hinge_barrel','iron')
        for i,a in enumerate(outline):
            edge('lid_edge', a, outline[(i+1)%6], .88, .07, .045, 'iron', True)
        for y in (-.65,.5):
            width = .83 if y<0 else 1.16
            box('lid_band',(0,y,.89),(width,.105,.035),'iron', True)
            for x in (-width*.40,width*.40):
                bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=.028, location=(x,y,.92))
                finish(bpy.context.object,'rivet','iron',True)
        # Angular bat crest inset in the lid.
        bat = [(-.34,.1),(-.18,.04),(-.12,.12),(0,.04),(.12,.12),(.18,.04),(.34,.1),(.22,-.1),(.1,-.08),(0,-.24),(-.1,-.08),(-.22,-.1)]
        slab('bat_crest',bat,.88,.025,'iron',True)
    export(name)
    # Editable source includes a keyed hinge demonstrating the same open/close motion as runtime.
    if vampire:
        pivot = bpy.data.objects.new('lid_hinge',None)
        bpy.context.collection.objects.link(pivot)
        pivot.location = hinge
        bpy.context.view_layer.update()
        for ob in objects:
            if ob['lid']:
                matrix=ob.matrix_world.copy()
                ob.parent=pivot
                ob.matrix_world=matrix
        for frame, angle in [(1,-105),(30,0),(90,0),(120,-105)]:
            pivot.rotation_euler.y=math.radians(angle)
            pivot.keyframe_insert(data_path='rotation_euler',frame=frame)
        scene.frame_end=120
        scene.frame_set(30)
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/(name+'.blend')))
    scene.render.filepath=str(ASSETS/(name+'.png'))
    bpy.ops.render.render(write_still=True)
    if vampire:
        scene.frame_set(1)
        scene.render.resolution_x=scene.render.resolution_y=700
        camera.data.ortho_scale=4.4
        camera.rotation_euler = (Vector((-.35,0,.8))-camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(HERE/'vampire_coffin_open.png')
        bpy.ops.render.render(write_still=True)
