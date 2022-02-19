# MESS - Entity rewrite rules

Entity rewrite rules are used to modify entity attributes before any macro entity processing takes place. This makes it possible to define custom entities for commonly used templates.


## Table of contents

- [Rewrite rules](#rewrite-rules)
    - [Rule types](#rule-types)
    - [Conditional rule blocks](#conditional-rule-blocks)
    - [Notes](#notes)
- [Example](#example)
- [Use-cases](#use-cases)
    - [Custom entities for template maps](#custom-entities-for-template-maps)
    - [Aliasing other entities](#aliasing-other-entities)
    - [Decorating other entities](#decorating-other-entities) 
    - [Upgrading to real entities](#upgrading-to-real-entities)   


## Rewrite rules
Rewrite rules are stored in `.fgd` files, in specially formatted comments. Rule blocks start with a `// @MESS REWRITE:` line and end with a `// @MESS;` line, and they apply to the first entity definition that comes after these comments.

### Rule types
There are two types of rules:

- `// "attribute-name": "new-value"` - This will overwrite the specified attribute with a new value.
- `// delete "attribute-name"` - This will delete the specified attribute.

Note that both attribute names and values can contain MScript expressions. Just as elsewhere, expressions are surrounded by curly braces.

### Conditional rule blocks
Rule blocks can also contain conditional blocks, whose rules are only applied if the condition holds:

- `// @IF "{condition}"` starts a conditional rule block. Conditional blocks cannot be nested.
- `// @ELSE:` starts an alternate rule block. The rules in this block are only applied if the `@IF` condition does not hold. Else blocks are optional.
- `// @ENDIF;` marks the end of a conditional rule block.

### Notes

- Only the rewrite rules for the original type (`classname`) of an entity are applied. So if an entity is rewritten to a different type, then any rules that apply to its new type will not be applied to it.
- The attributes of the entity that is being rewritten are available to MScript expressions. Referencing an attribute will return its current value.
- Rewrite rules are applied in the same order as they appear in the `.fgd` file. Multiple rules can be applied to the same attribute.  

## Example
The following example defines a `monster_landmine` entity, along with rewrite rules that will turn any such entity into a `macro_insert` entity that references a specific template map. Conditional rule blocks are used to select a different template based on the entity's `type` attribute.

    // @MESS REWRITE:
    // "classname": "macro_insert"
    // @IF "{type == 0}":
    //   "template_map": "{dir()}\hidden_landmine.rmf"
    // @ELSE:
    //   "template_map": "{dir()}\landmine.rmf"
    // @ENDIF;
    // @MESS;
    @PointClass = monster_landmine
    [
        damage(integer) : "Damage" : 100
        type(choices) : "Type" =
        [
            0 : "Hidden"
            1 : "Exposed"
        ]
    ]

Note the `dir()` function call in the `template_map` rewrite rules: this returns the directory specified by the `-dir` command-line argument (or the directory that the map that is being processed is in, if there's no `-dir` argument). So if MESS was started with `-dir "C:\Users\myusername\Documents\Half-Life maps\templates"`, then `monster_landmine` entities will be replaced with the contents of `C:\Users\myusername\Documents\Half-Life maps\templates\landmine.rmf` (or the hidden variant).

It's a good idea to create a single root template maps folder, and to always pass that directory to MESS with `-dir`. This ensures that MESS can always find the right template map.

## Use-cases

### Custom entities for template maps
Rewrite rules were created to make it possible to create custom entities for commonly used templates. For example, manually adding a `macro_insert` entity, pointing it at the `templates\monster_warp.rmf` template map, and disabling SmartEdit mode for further configuration can be error-prone. A custom `monster_warp` entity with properly defined attributes is easier to use.

### Aliasing other entities
Another use-case is to create aliases for existing entities. For example, one of the best entities for adding decorative models to a Half-Life level is  actually `env_sprite` - but because it's a sprite entity, it's somewhat cumbersome to use and can cause certain editors to crash. An `env_model` alias with a `model(studio)` attribute and a rewrite rule that turns it into an `env_sprite` makes this workaround much easier to use.

### Decorating other entities
Sometimes it's desirable to add small accentuating lights to items, such as a subtle blue glow for batteries or a white light for healthkits. This can be automated by rewriting `item_battery` and `item_healthkit` to a template-inserting entity, with templates that contain both an item and a light.

An important detail here is that items in template maps should not be rewritten - otherwise the template maps will insert themselves, causing an infinite loop (until MESS hits the `-maxrecursion` or `-maxinstances` limit). This can be achieved by putting the rewrite rules in an `@IF "{not norewrite}":` block, and adding a `norewrite` attribute to the items in the template maps.

### Upgrading to real entities
Rewrite rules also make it easier to 'upgrade' template entities to real entities. For example, a mod developer decides to add an `env_model` or `monster_warp` entity to their mod's game code. Instead of having to replace several `macro_insert` entities, all they need to do is to remove the rewrite rules for the upgraded entities. Maps will then use the 'upgraded' entity the next time they are compiled.