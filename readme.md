# MESS - Macro Entity Scripting System
*Ah, Freeman, I see you are in this mess too!*


## Table of contents

- [Introduction](#introduction)
- [Quick examples](#quick-examples)
- [How to install MESS](#how-to-install-mess)
    - [Command-line arguments](#command-line-arguments) 
- [How to use MESS](#how-to-use-mess)
    - [Macro entities](#macro-entities) - (see also: [macro entities.md](macro%20entities.md))
    - [Entity rewrite rules](#entity-rewrite-rules) - (see also: [entity rewrite rules.md](entity%20rewrite%20rules.md))
    - [Scripting](#scripting) - (see also: [scripting system.md](scripting%20system.md))

## Introduction
MESS is a Half-Life level compile tool that helps automating various tasks. It provides a **templating system** and several **macro entities** for creating template instances, which can be customized with a basic **scripting system**. And with **entity rewrite rules**, templates can be used as if they were actual entities.

Here are some of the things that MESS can do:

- Duplicating complex entity setups as if they were a single entity.
- Covering terrain and other surfaces with props.
- Turning a single brush into multiple entities.

*Fun fact: inspiration for this tool came from the game `Baba is you` and the programming language `Lisp`.*

## Quick examples

This is what a MESS template looks like and how it can be used:

![Landmine template](/documentation/images/landmine%20template.png "Landmine template")
![Minefield](/documentation/images/landmine%20insertion.png "Minefield")

Anything between the `macro_template` brushes is part of the `landmine` template. When MESS processes the map, a copy of the `landmine` template is created for each `macro_insert`. Because the `env_explosion` magnitude uses the expression `{damage or 100}`, landmines have a default magnitude of 100, but each `macro_insert` can override that with the custom `damage` attribute.

Also note the use of the `{id()}` expression: this gives the entities of each landmine distinct names. Otherwise, each trigger would active all explosions, instead of only the explosion it belongs to.

Here is another MESS entity in action, `macro_cover`:

![Covering terrain](/documentation/images/macro_cover%20landscape.png "Covering terrain")

This entity randomly places template instances across its non-NULL surfaces. The 6 templates here all have the same name, so each time a random one is selected. The orientation and scale of each instance has also been randomized with expressions.


## How to install MESS
The following instructions assume that you're using the Hammer or J.A.C.K. level editor, but the overall process should be fairly similar for other editors.

1. Download MESS and extract the contents to a folder (this folder is referred to below as `<MESS FOLDER>`). Optionally, download the example maps and extract them to your maps folder (referred to below as `<MAPS FOLDER>`).
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
4. For [**rewrite rules**](entity%20rewrite%20rules.md):
    1. Add `MESS_rewrite_rule_examples.fgd` to your game configuration or game profile (see step 2).
    2. Add the `-dir "<MAPS FOLDER>\templates"` and `-fgd "<MAPS FOLDER>\MESS_rewrite_rule_examples.fgd"` parameters to the MESS compile step (see step 3). These parameters must come before `"$path/$file.$ext"`. The parameters field should now look like this: `-dir "<MAPS FOLDER>\templates" -fgd "<MAPS FOLDER>\MESS_rewrite_rule_examples.fgd" "$path/$file.$ext"`.

### Command-line arguments
By default, MESS will overwrite the given .map file. If you want to save the result to a different file, just add another path: `"$path/$file.$ext" "<OUTPUT PATH + FILENAME>"`. Additionally, MESS takes the following options (which must be added before the input and output parameters):

- **-dir *directory*** - Specifies which directory to use when resolving relative template map paths. By default, the directory where the input map file is located is used. This only applies to relative template paths in the input map file.
- **-fgd *paths*** - The .fgd file(s) that contain MESS entity rewrite rules. To provide multiple paths, separate them with a semicolon (`path1;path2`). 
- **-maxrecursion *number*** - The maximum recursion depth for templates that insert themselves or other templates. This is a safety mechanism that guards against accidental infinite recursion. The default is 100. 
- **-maxinstances *number*** - The maximum number of template instances. This is a safety mechanism that guards against accidentally inserting a massive number of instances. The default is 10000.
- **-log *level*** - Determines how much information MESS will write to the output:
    - **off** - Disables almost all logging.
    - **error** - Only critical errors are shown (problems that cause MESS to abort). This is the default.
    - **warning** - In addition to critical errors, warnings are also shown (problems that MESS can safely ignore).
    - **info** - Additional information is shown.
    - **verbose** - The maximum amount of information is shown.
- **-repl** - Enables the interactive MScript interpreter mode. This starts a read-evaluate-print loop (REPL), which can be used to test MScript expressions. *Do not use this when compiling maps!*


## How to use MESS
### Macro entities
Almost everything in MESS involves templates. A template can be a separate map, or it can be created with the following entities (for more detailed information, see [macro entities.md](macro%20entities.md)):

- **macro\_template** - Anything inside the bounding box of this entity becomes part of a template. Templates are removed from the map, but instances can then be created in various other places by other macro entities.
- **macro\_remove\_if** - Used inside templates. When an instance of a template is created, anything inside the bounding box of this entity is excluded from that instance if the removal condition is true.

The following entities can be used to create one or more template instances:

- **macro\_insert** - A point entity that creates a single template instance. The attributes of this entity are visible to script expressions in the selected template, so each instance can be made unique. Useful for duplicating complex entity setups.
- **macro\_cover** - A brush entity that creates multiple template instances, using them to cover its non-NULL faces. Useful for covering terrain with props.
- **macro\_fill** - A brush entity that creates multiple template instances inside its brushes. Useful to fill an area with particles or other things.

There is also one more entity that uses templates in a slightly different way:
  
- **macro\_brush** - A brush entity that creates copies of its own brushwork, with each copy taking on the textures and entity attributes of a brush or entity in the selected template. Useful for creating fences, or for adding visual effects to triggers.

### Entity rewrite rules
Rewrite rules can be used to modify entity attributes before macro processing occurs. This makes it possible to create custom entities for template maps. For more detailed information, see [entity rewrite rules.md](entity%20rewrite%20rules.md).

For example, the landmines above were created with a `macro_insert` entity that referenced the `landmine` template. To modify the landmine's damage, a specific attribute had to be added with SmartEdit mode. With entity rewrite rules, it's possible to create a `monster_landmine` entity with a proper `damage` attribute.

Entity rewrite rules are created by adding specially formatted comments to an `.fgd` file, right before the entity type that they apply to:

    // @MESS REWRITE:
    // "classname": "macro_insert"
    // "template_map": "{dir()}\landmine.rmf"
    // @MESS;
    @PointClass = monster_landmine
    [
        damage(integer) : "Damage" = 100
    ]

The above rules will turn any `monster_landmine` entity into a `macro_insert` entity, with its `template_map` attribute set to reference the landmine template map. The resulting macro entity is then processed by MESS as usual.

### Scripting
Below is a quick overview of MESS' scripting capabilities. For more detailed information, see [scripting system.md](scripting%20system.md).

#### Embedding expressions in attribute values
Any entity attribute can be customized with embedded expressions. These expressions are surrounded by curly braces. For example, the value `corner{4 + 5}` contains the expression `4 + 5`, which evaluates to `9`, so the final value becomes `corner9`.

#### Referencing attributes of instance-creating entities 
When a macro entity creates a template instance, its attributes are made visible to expressions in the selected template. This means that expressions inside a template can produce different results depending on the attributes of the macro entity that uses that template.

This is why, in the above landmine example, each `macro_insert` can specify its own landmine damage: the `damage` in `{damage or 100}` refers to the `damage` attribute of the current `macro_insert` entity.

#### SmartEdit mode
Enabling `SmartEdit` mode allows new attributes to be added, which is very useful for customizing templates. That's how, in the landmine example, the custom `damage` attribute was added.

But there's another reason why `SmartEdit` mode is useful: just as Half-Life itself, MESS only recognizes internal attribute names, and these are only shown when `SmartEdit` mode is enabled.

#### Commonly used functions
Finally, here are some of the most commonly used functions that can be used in MESS expressions:

- `iid()` - Returns the unique numeric ID of the current instance.
- `id()` - Returns either the `targetname` of the instance-creating entity, or the unique instance ID. This is a more convenient way of writing `{targetname or iid()}`.
- `rand(min, max)` - Returns a random number between `min` and `max`.
- `randi(min, max)` - Returns a random integer number between `min` (inclusive) and `max` (exclusive).

`rand` and `randi` can be called with no arguments: `rand` will then return a number between 0 and 1, and `randi` will return either 0 or 1. They can also be called with only one argument, which will then be used as `max` value (`min` will be set to 0).