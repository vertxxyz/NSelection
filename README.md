# NSelection
Simple selection for busy scenes in Unity

**Unity 2018.3+**

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/Z8Z42ZYHB)

----
## Usage:
### Scene view picking
<kbd>Ctrl</kbd> **- right-mouse** to activate.  
Hold <kbd>Ctrl</kbd> during selection to select multiple objects.  
Press <kbd>Escape</kbd> or **click** outside the selection list to end the current selection.  

![gif](http://vertx.xyz/Images/NSelection/nSelection4.gif)

### Drag and drop between tabs
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

## Installation

<details>
<summary>Add from OpenUPM <em>| via scoped registry, recommended</em></summary>

This package is available on OpenUPM: https://openupm.com/packages/com.vertx.nselection

To add it the package to your project:

- open `Edit/Project Settings/Package Manager`
- add a new Scoped Registry:
  ```
  Name: OpenUPM
  URL:  https://package.openupm.com/
  Scope(s): com.vertx
  ```
- click <kbd>Save</kbd>
- open Package Manager
- click <kbd>+</kbd>
- select <kbd>Add from Git URL</kbd>
- paste `com.vertx.nselection`
- click <kbd>Add</kbd>
</details>

<details>
<summary>Add from GitHub | <em>not recommended, no updates through UPM</em></summary>

You can also add it directly from GitHub on Unity 2019.4+. Note that you won't be able to receive updates through Package Manager this way, you'll have to update manually.

- open Package Manager
- click <kbd>+</kbd>
- select <kbd>Add from Git URL</kbd>
- paste `https://github.com/vertxxyz/NSelection.git`
- click <kbd>Add</kbd>  
**or**  
- Edit your `manifest.json` file to contain `"com.vertx.nselection": "https://github.com/vertxxyz/NSelection.git"`,
  
To update the package with new changes, remove the lock from the `packages-lock.json` file.
</details>
