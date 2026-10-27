import unreal
s=unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
print("PIE before", s.is_in_play_in_editor())
s.editor_request_end_play()
print("PIE req end")
