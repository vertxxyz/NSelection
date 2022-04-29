# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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