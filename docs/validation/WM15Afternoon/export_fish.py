import bpy
bpy.ops.wm.open_mainfile(filepath='C:/serenity_game/Artifacts/WM15Afternoon/FishSource/fish.blend')
a=bpy.data.objects['Armature'];a.animation_data_create();a.animation_data.action=bpy.data.actions['swim']
if a.animation_data.action.slots:a.animation_data.action_slot=a.animation_data.action.slots[0]
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=23
for o in bpy.context.scene.objects:
 o.hide_set(False);o.hide_viewport=False;o.select_set(o.type in {'MESH','ARMATURE'})
bpy.ops.export_scene.fbx(filepath='C:/serenity_game/Assets/Game/Art/WM15Afternoon/Imported/Fish/Fish.fbx',use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='STRIP',axis_forward='-Z',axis_up='Y')
print('EXPORTED_SWIM',len(a.animation_data.action.fcurves))
