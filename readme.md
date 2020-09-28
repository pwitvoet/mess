# MESS - Macro Entity Scripting System
*Ah, Freeman, I see you are in this mess too!*


## Introduction
MESS is a Half-Life level compile tool that helps automating various tasks. It provides a **templating system** and several **macro entities** for creating template instances, which can be customized with a basic **scripting system**.

Here are some of the things that MESS can do:

- Duplicating complex entity setups as if they were a single entity.
- Covering terrain and other surfaces with props.
- Turning a single brush into multiple entities.

*Fun fact: inspiration for this tool came from the game `Baba is you` and the programming language `Lisp`.*


## How to install MESS
The following instructions assume that you're using the Hammer or J.A.C.K. level editor, but the overall process should be fairly similar for other editors.

1. Download MESS and extract the contents to a folder (this folder is referred to below as `<MESS FOLDER>`).
2. Add the **game data file (mess.fgd)** to your editor.
  1. Go to the `Tools` menu and select `Options`.
  2. In the configuration window that opens, go to the `Game Configurations` or `Game Profiles` tab.
  3. Look for the `Game Data Files` section, click the `Add` button and select `<MESS FOLDER>\mess.fgd`.
3. Add a **compile step (mess.exe)** to your editors `Compile/run commands`:
  1. Click the `Run map! [F9]` or `Run the map in the game [F9]` button and switch to `Expert mode`.
  2. In the `Compile/run commands` list, click `New` to create a new build step and press `Move Up` or `Up` until it's at the top.
  3. In the `Command:` field, enter `<MESS FOLDER>\MESS.exe`.
  4. In the `Parameters:` field, enter `"$path/$file.$ext"`.
  5. For J.A.C.K. users: tick the `Wait for Termination` and `Use Process Window` checkboxes.

### Command-line arguments
By default, MESS will overwrite the given .map file. If you want to save the result to a different file, just add another path: `"$path/$file.$ext" "<OUTPUT PATH + FILENAME>"`. Additionally, MESS takes the following options (which must be added before the input and output parameters):

- **-dir *directory*** - Specifies which directory to use when resolving relative template map paths. By default, the directory where the input map file is located is used. This only applies to relative template paths in the input map file.
- **-maxrecursion *number*** - The maximum recursion depth for templates that insert themselves or other templates. This is a safety mechanism that guards against accidental infinite recursion. The default is 100. 
- **-maxinstances *number*** - The maximum number of template instances. This is a safety mechanism that guards against accidentally inserting a massive number of instances. The default is 10000.
- **-log *level*** - Determines how much information MESS will write to the output:
  - **off** - Disables almost all logging.
  - **error** - Only critical errors are shown (problems that cause MESS to abort). This is the default.
  - **warning** - In addition to critical errors, warnings are also shown (problems that MESS can safely ignore).
  - **info** - Additional information is shown.
  - **verbose** - The maximum amount of information is shown. 


## How to use MESS
### Macro entities
Almost everything in MESS involves templates. They can be created with the following entities:

- **macro\_template** - Anything inside the bounding box of this entity becomes part of a template. Templates are removed from the map, but instances can then be created in various other places by other macro entities.
- **macro\_remove\_if** - Used inside templates. When an instance of a template is created, anything inside the bounding box of this entity is excluded from that instance if the removal condition is true.

The following entities can be used to create one or more template instances:

- **macro\_insert** - A point entity that creates a single template instance. The attributes of this entity are visible to script expressions in the selected template, so each instance can be made unique. Useful for duplicating complex entity setups.
- **macro\_cover** - A brush entity that creates multiple template instances, using them to cover its non-NULL faces. Useful for covering terrain with props.
- **macro\_fill** - A brush entity that creates multiple template instances inside its brushes. Useful to fill an area with particles or other things.

There is also one more entity that uses templates in a slightly different way:
  
- **macro\_brush** - A brush entity that creates copies of its own brushwork, with each copy taking on the textures and entity attributes of a brush or entity in the selected template. Useful for creating fences, or for adding visual effects to triggers.

### Scripting
#### Embedding expressions in attribute values
Any entity attribute can be customized with embedded expressions. These expressions are surrounded by curly braces. For example, the value `corner{4 + 5}` contains the expression `4 + 5`, which evaluates to `9`, so the final value becomes `corner9`.

#### Referencing attributes of instance-creating entities 
When a macro entity creates a template instance, its attributes are made visible to expressions in the selected template. For example, imagine a template that contains an `env_sprite` entity with its `renderamt` (FX Amount) attribute set to `{brightness or 128}`. The `brightness` part means that this will use the value of the `brightness` attribute of the instance-creating entity. The `or 128` part means that, if the `brightness` attribute is empty or missing, `128` will be used instead.

So a `macro_insert` entity that contains a `brightness` attribute with value `0` will produce an `env_sprite` with a `renderamt` of `0`, while a `macro_insert` without a `brightness` attribute will produce an `env_sprite` whose `renderamt` is `128`.

#### SmartEdit mode
MESS only recognizes internal attribute names, which are only visible in `SmartEdit` mode. But `SmartEdit` mode also allows new attributes to be added, which is very useful for customizing templates.

#### Functions
Finally, here are some of the most commonly used functions that can be used in MESS expressions:

- `iid()` - Returns the unique numeric ID of the current instance.
- `id()` - Returns either the `targetname` of the instance-creating entity, or the unique instance ID. This is a more convenient way of writing `{targetname or iid()}`.
- `rand(min, max)` - Returns a random number between `min` and `max`.
- `randi(min, max)` - Returns a random integer number between `min` (inclusive) and `max` (exclusive).

`rand` and `randi` can be called with no arguments (`rand` will then return a number between 0 and 1, and `randi` will return either 0 or 1). They can also be called with only one argument, which will then be used as `max` value (`min` will be set to 0).