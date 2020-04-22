MESS - Macro Entity Substitution System
=======================================


TODO LIST:

v macro_insert_map: inserts another map into the current one
  v 'map' property specifies other map, path is relative to current map
  v 'scale' property scales everything up (brush positions and sizes, entity positions)
  v 'angles' rotates everything (brush positons and sizes, entity positions)
  - Detect and prevent cyclic references.
  - Fix textures (offset/scale/rotation).
  - Support automatic extension detection (first try .rmf, then try .map, unless an extension is explicitly provided).
  - Support randomly selected variants: a * in a map path will act as a wildcard. A map will be randomly chosen from all possible matches.
    Only select .rmf's, but if there are no matching .rmf's, try .map's - unless an extension is explicitly provided. Extension cannot have a wildcard, maps can.
  - Add support for a special {#ID} placeholder, which is replaced by a unique ID. Useful for giving each instance their own distinct entity names,
    when you don't care about referencing those entities from outside a template. With this, you don't need to give each macro_insert_map entity a unique name or other unique substitution value.
  - Make scale have an x, y and z component, instead of a uniform scale.
  - Cancel the whole process on error (such as map not found), show clear error messages, provide sufficient detail for user to be able to fix the problem quickly (which template, into which map, entity position, etc.)
.


FUTURE IDEAS:

- macro_insert_visgroup: Similar to macro_insert_map, this one duplicates a VIS group from the current map. Makes it easier to modify/preview the template.
  - VIS groups can be named, groups cannot, so that's why this will use VIS groups.
  - Alternately, a macro_name point entity could be included, and anything it's being grouped with will serve as a template that can be referred to...
  - 
- brush-based entities that semi-randomly (deterministically) fill a volume or cover a face with instances of another map. Useful for small details like plants, protruding bricks, clouds.
  - randomizing scale and angle, with range expressions: "angles"="0 {random(0, 360)} 0", or "{randomBellCurve(0, 90)}" (think about syntax some more though?)
  - allow orienting instances based on face normal
  - allow clamping positions to certain grid size (perhaps expression-based for more flexibility?)
- conditional exclusion of certain entities/brushes. To make small variations easier to make.
  - Could be done by adding a custom property to entities: 'is_excluded'='{CONDITION}'
  - For world geometry, wrap them in a macro_world_geometry entity. The entity will turn its content into world brushes (or not, if is_excluded evaluates to true).
.

- Basic expression support: 'angles': '{angles.x + 90} {angles.y} 0' would copy the x and y angles of the 'parent' macro entity, adding 90 degrees to the x axis, but ignoring the z axis.
  - identifiers (refer to 'parent' entity properties)
  - primitives: numbers, strings, booleans
  - arithmetic operators: + - * / %
  - logical operators: == != > >= < <=
  - a few standard functions:
    - id(), for the auto-generated ID of the currently expanding macro entity
	- random-related methods
	- string methods
	- ...?
.