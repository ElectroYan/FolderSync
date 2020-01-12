# FolderSync
Simple library to sync two folders

This library only syncs one directional and is meant to back up huge amounts of data without windows interfering with annoying errors and pop-ups.

Modes:

- Copy - Basically like drag'n'dropping the folder to the new destination but without mentioned problems. Overwrites all existing files if they've changed (based on 'date modified').
- CopyAndDelete - Like copy but if any file exists in the destination path which doesn't exist in the source path it gets deleted. This makes sure there's no clutter of old/deleted files.


I do not guarantee that this works 100% flawlessly.
