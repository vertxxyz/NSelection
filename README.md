# NSelection
Simple selection for busy scenes in Unity.  
Plus other simple additions towards editor usability.

> **Warning**  
> Unity 2019.4+

## Usage:
### Scene view picking
<kbd>Ctrl</kbd> **- right-mouse** to activate.  
Hold <kbd>Ctrl</kbd> during selection to select multiple objects.  
Press <kbd>Escape</kbd> or **click** outside the selection list to end the current selection.  

![gif](http://vertx.xyz/Images/NSelection/nSelection4.gif)

### Drag and drop between tabs
(This is now built-in for Unity 2023.2+!)  
![gif](http://vertx.xyz/Images/NSelection/nSelectionDragging.gif)

### Focus hierarchy to selection
<kbd>Alt+Shift+F</kbd> sets the hierarchy's expanded state to only contain the current selection.  
You can rebind this in the Shortcut Manager (**Edit/Shortcuts/Window**).  
The supported windows are:
- **Hierarchy view**
- **Project browser**
- **Profiler window**
- **Timeline window**
- **Animation window**
- **Audio Mixer**
- **UI Builder**
- **UIToolkit debugger**
- **Frame debugger**

### Collapse Hierarchy
Assign the Collapse Hierarchy shortcuts in the Shortcut Manager (**Edit/Shortcuts/Hierarchy View**).  
- **Collapse Hierarchy** will collapse anything but expanded Scenes and the selection.
- **Collapse Hierarchy Completely** will collapse everything.

### Project Browser Create/Script
A shortcut for creating scripts in the project browser. Assign in (**Edit/Shortcuts/Project Browser**).

### Scene View/Toggle Gizmos
A shortcut for to toggle gizmos in the scene view. <kbd>G</kbd> by default. Assign in (**Edit/Shortcuts/Scene View**).

## Installation

[![openupm](https://img.shields.io/npm/v/com.vertx.nselection?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.vertx.nselection/)

<table><tr><td>

#### Add the OpenUPM registry
1. Open `Edit/Project Settings/Package Manager`
1. Add a new Scoped Registry (or edit the existing OpenUPM entry):
   ```
   Name: OpenUPM
   URL:  https://package.openupm.com/
   Scope(s): com.vertx
   ```
1. **Save**

#### Add the package
1. Open the Package Manager via `Window/Package Manager`.
1. Select the <kbd>+</kbd> from the top left of the window.
1. Select **Add package by Name** or **Add package from Git URL**.
1. Enter `com.vertx.nselection`.
1. Select **Add**.

</td></tr></table>

If you find this resource helpful:

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/Z8Z42ZYHB)