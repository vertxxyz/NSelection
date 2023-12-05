# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.3.0]
- 2023.3 updates:
  - Added shortcut, to resolve conflicts with new piercing selection menu.
  - Using the Scene view picker with an active selection no longer clears it.
- Reduced hierarchy expansion in specific multi-select cases.
- Fixed incorrect window position when scrolling.

## [1.2.0]
- Removed tab dragging code from 2023.2+ as it's now built-in!
- Added a shortcut for toggling gizmos in the scene view. Defaults to G. If you're using ProBuilder this will be a conflict. Assign or resolve in Edit/Shortcuts/Scene View.
- Added a shortcut for creating scripts in the project browser. Assign in Edit/Shortcuts/Project Browser.
- Increased minimum version to 2019.4. It's likely this is late, sorry!

## [1.1.4] - 2022-06-19
- Fixed errors when missing Timeline package.

## [1.1.3] - 2022-05-14
- Added support for 2022.2.0a13. If you are on an earlier version of 2022.2.0, this update will conflict.

## [1.1.2] - 2022-04-29
- Fixed Focus hierarchy to selection falling back to the scene view hierarchy when an asset is focused in the Inspector.
- Fixed Focus hierarchy to selection not repainting unfocused windows.

## [1.1.1] - 2022-04-17
- Added Focus hierarchy to selection (Alt+Shift+F). This is likely more useful than Collapse Hierarchy. It supports:
  - Hierarchy view
  - Project browser
  - Profiler window
  - Timeline window
  - Animation window
  - Audio Mixer
  - UI Builder
  - UIToolkit debugger
  - Frame debugger
- Fixed Collapse Hierarchy to properly collapse selected element.

## [1.1.0] - 2022-04-13
- Added unbound shortcuts for collapsing the hierarchy, (I recommend binding to Ctrl-Backspace).
  - Collapse Hierarchy will collapse anything but expanded Scenes and the selection.
  - Collapse Hierarchy Completely will collapse everything.
- Added many exposed methods through NSelection.
- Changed multi-select shortcuts to be consistent with common control schemes.
- Fixed inconsistent sizing issues that are especially present on scaled displays.
- Changed DraggingAdditions to be internal.

## [1.0.5] - 2021-07-23
- Fixed annoying behaviour when no item is found under the cursor.

## [1.0.3] - 2019-12-14
- Added support for 2020
- Dragging Additions can be disabled with the DISABLE_DRAGGING_ADDITIONS define

## [1.0.2] - 2019-09-16
- Made dragging objects to inspector tabs switch between them

## [1.0.1]
- Fix for null components
- Update to 2018.3

## [1.0.0]
- Initial releases