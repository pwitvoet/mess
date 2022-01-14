# MESS - Scripting system

MESS contains a small scripting language named MScript. Its syntax is similar to that of Python and Javascript.


## Table of contents

- [Usage](#usage)
    - [Embedded expressions](#embedded-expressions)
    - [Parent entities](#parent-entities)
    - [Flags](#flags)
- [Common expressions](#common-expressions)
- [Data types](#data-types)
    - [number](#number)
    - [vector](#vector)
    - [string](#string)
    - [none](#none)
- [Operators](#operators)
    - [Arithmetic](#arithmetic)
    - [Equality](#equality)
    - [Comparisons](#comparisons)
    - [Logical](#logical)
    - [Negation](#negation)
    - [Conditional](#conditional)
    - [Parentheses](#parentheses)
- [Functions](#functions)
    - [Entity ID](#entity-id)
    - [Randomness](#randomness)
    - [Mathematics](#mathematics)
    - [Trigonometry](#trigonometry)
    - [Colors](#colors)
    - [Flags](#flags)
    - [Globals](#globals)
    - [Directories](#directories)

## Usage
### Embedded expressions
MScript expressions can be embedded in entity attribute names or values by surrounding them with curly braces. For example, `fire_{4 + 5}` contains the expression `4 + 5`, which evaluates to `9`, so the resulting value will be `fire_9`.

### Parent entities
When a macro entity is instantiating a template, it is seen as the 'parent entity'. Entities within the template are given access to the attributes of this parent entity, so an attribute with the value `fire_{targetname}` is turned into `fire_box1` if the parent entity's `targetname` is `box1`. This works for any attribute, including custom ones. Because MESS only knows about 'internal' attribute names, it's a good idea to disable `SmartEdit` mode.

The `worldspawn` entity acts as parent entity for anything that is not part of a template. This means that map properties can be used in expressions.

### Flags
Flags can be set by adding special `spawnflag<N>` attributes to an entity, where `<N>` is a number between 0 (the first flag) and 31 (the last flag). If the value is `none` (empty) or `0` then the associated flag will be disabled, else it will be enabled. For example, a `spawnflag2` attribute with value `{5 > 4}` will enable the 3rd flag, whereas a `spawnflag3` attribute with value `{2 > 4}` will disable the 4th flag.


## Common expressions

- If you want to make an attribute customizable, but also provide a default value, use `or`: `{parent_entity_attribute_name or default_value}`.
- If you want to prevent multiple template instances from triggering each other's entities, use the `iid()` function (short for 'instance ID') to ensure instance-specific entity names: `my_entity_{iid()}`.
- Same as the above, but if you want instance-creating entities to be able to set a specific entity name, use the `id()` function instead (`{id()}` is the same as `{targetname or iid()}`).
- If you want part of a template to only be included by the first instance of that template, then surround it with a `macro_remove_if` and use `{useglobal('UNIQUE_NAME')}` as removal condition (where `UNIQUE_NAME` should be a unique name).


## Data types
MScript supports the following data types. The type of a value determines what sort of operations can be performed with it.

### `number`
Used for attributes that describe amounts, distances and volumes, but also for attributes where a value can be selected from a list, because these are stored as numbers internally. Spawn flags are also stored as a number, in the special `spawnflags` attribute. They are written the same both inside and outside expressions.

Numbers can be used in arithmetic operations and can be compared against each other.


### `vector`
An array of numbers, used for rotation angles and color attributes. In entity attributes these are normally written as 3 or 4 numbers, separated by spaces: `1 2 3`, which is also how they are represented when converted to a string. However, in MScript they are written as `[1, 2, 3]`.

Vectors can be indexed, with the first number being at position 0: `[1, 2, 3][0]` produces `1`. MScript also supports negative indexes, with `-1` starting at the last number. Accessing an index that does not exist produces the special value `none` (see below).

#### Properties
Vectors are mostly used for rotation angles, colors and positions, which is reflected by their properties:

- `number pitch` - same as `vector[0]`
- `number yaw` - same as `vector[1]`
- `number roll` - same as `vector[2]`
- `number r` - same as `vector[0]`
- `number g` - same as `vector[1]`
- `number b` - same as `vector[2]`
- `number brightness` - same as `vector[3]`
- `number x` - same as `vector[0]`
- `number y` - same as `vector[1]`
- `number z` - same as `vector[2]`
- `number length` - how many numbers a vector contains: `[0, 0, 0].length` produces `3`.


### `string`
A piece of text. Any attribute value that does not look like a number or a sequence of numbers is treated as a string. Within expressions strings must be surrounded by single quotes, to distinguish them from identifiers: `'targetname'` produces the literal text `targetname`, whereas `targetname` would produce the targetname of the macro entity that is instantiating the current template.

Strings can be indexed in much the same way as vectors can, except that indexing returns a 1-character string instead of a number. Negative indexing is also supported, and indexes that do not exist produce the special value `none`.

#### Properties

- `number length`
    - Returns the length of a string: `'hello'.length` produces `5`.

#### Member functions
- `string substr(number offset, number length?)`
    - This function returns a specific part of a string. Offset can be negative, just as with indexing. Length is optional - if omitted, the rest of the string is returned, starting at offset. Unlike indexing, this returns an empty string if offset and length do not fall within the string boundaries.
- `bool contains(string str)`
    - Returns `1` (true) if the current string contains the given string, or `none` (false) if it does not.
- `bool startswith(string str)`
    - Returns `1` (true) if the current string starts with the given string, or `none` (false) if it does not.
- `bool endswith(string str)`
    - Returns `1` (true) if the current string ends with the given string, or `none` (false) if it does not.
- `string replace(string str, string replacement)`
    - Returns a string where all occurrences of `str` have been replaced with `replacement`.


### `none`
The special value `none` represents attributes that are empty or missing entirely. Within expressions it is written as `none`.

Unlike many other languages, where working with `null` or `None` often results in errors, `none` typically acts as 0 or a zero-filled vector in arithmetic operations.

In logical contexts, `none` acts as false, while anything that is not `none` acts as true.


## Operators
### Arithmetic
The arithmetic operators are:

- `a + b` - addition or string concatenation
- `a - b` - subtraction
- `a * b` - multiplication
- `a / b` - division
- `a % b` - remainder

#### Notes

- These operators work for both numbers and vectors. For example: `4 + 5` produces `9` and `[1, 2, 3] + [4, 5, 6]` produces `[5, 7, 9]`.
- If one operand is a vector and the other a number, then the number gets expanded into a vector. For example: `[1, 2, 3] + 4` is the same as `[1, 2, 3] + [4, 4, 4]`, and both produce `[5, 6, 7]`.
- If both operands are vectors, but one is shorter than the other, then the shortest one is padded with zeroes.
- If one operand is a number and the other is `none`, then `none` acts as 0 (zero).
- If one operand is a vector and the other is `none`, then `none` is expanded into a vector that contains zeroes.
- `+` acts as a string concatenation operator, if at least one of the operands is a string. If the other operand is not a string, then it is converted into its 'entity attribute representation'. For example, `'hello' + 9` produces `'hello9'`, `'test' + [1, 2, 3]` produces `'test1 2 3'` and `'world' + none` produces `'world'`.
- If both operands are `none`, then the result is always `none`.

### Equality
The equality operators are:

- `a == b` - equality
- `a != b` - unequality

#### Notes
 
- These operators return either `1` (true) or `none` (false).
- Values of different types are never equal to each other.

### Comparisons
The comparison operators are:

- `a > b` - greater than
- `a >= b` - greater than or equal to
- `a < b` - less than
- `a <= b` - less than or equal to

#### Notes

- These operators only work for numbers and for `none`, with `none` being treated as 0 (zero).

### Logical
The logical operators are:

- `a && b` - logical 'and' (can also be written as `a and b`)
- `a || b` - logical 'or' (can also be written as `a or b`)

#### Notes

- Only `none` is seen as false, any other value is seen as true.
- `and` returns the last operand, if both operands are true. Otherwise it returns `none`.
- `or` returns the first operand that is true. If both operands are false it returns `none`.
- `and` will not evaluate the second operand if the first operand is `none`. Likewise, `or` will not evaluate the second operand if the first operand is not `none`.

### Negation
The negation operators are:

- `-a` - numeric negation
- `!a` - logical negation (can also be written as `not a`)

#### Notes

- Numeric negation works for numbers, vectors and `none`:
    - `-9` produces `-9`
    - `-[1, 2, 3]` produces `[-1, -2, -3]`
    - `-none` produces `none`
- Logical negation produces `1` if the operand is `none`, and `none` if the operand is any other value.

### Conditional
The conditional operator is:

- `condition ? a : b` - conditional (can also be written as `a if condition else b`)

#### Notes

- If `condition` is true (again, anything but `none` is true) then this produces the `a` operand, otherwise it produces the `b` operand.
- Only the chosen operand is evaluated.

### Parentheses
Parentheses can be used to control the order in which operations are evaluated, overruling their default associativity and precedence. For example, `2 + 4 * 5` evaluates to `22`, because `*` has a higher precedence than `+`, so `4 * 5` is evaluated first. But `(2 + 4) * 5` evaluates to `30`, because the parentheses force the `2 + 4` part to be evaluated first.

- `(expression)` - same as `expression`


## Functions
MESS provides a small number of 'standard library' functions.

### Entity ID:

- `number iid()`
    - Returns the numeric ID of the current instance. When used inside an entity rewrite rule, returns the ID of the current entity.
- `string id()`
    - Returns either the `targetname` of the macro entity that is creating the current instance, or the numeric ID (as a string) of the current instance. Shorthand for `targetname or (iid() + '')`.

### Randomness:
To ensure the exact same result each time a map is compiled, randomness in MESS is not actually random but pseudo-random. To get different results, it's possible to provide a different 'seed' value.

The default seed value is 0. To provide a different seed, add a `random_seed` attribute to the map properties (this affects expressions in rewrite rules and expressions in entities that are not part of a template) or to a `macro_insert`, `macro_cover`, `macro_fill` or `macro_brush` entity (this affects expressions in entities that are part of the chosen template).

- `number rand(number? min, number? max)`
    - Returns a random number between `min` and `max`.
    - If only one argument is specified, then `min` is set to `0.0` and the argument is used as `max`.
    - If no arguments are specified, then the function returns a random number between `0.0` and `1.0`.
- `number randi(number? min, number? max)`
    - Returns a random integer number between `min` (inclusive) and `max` (exclusive).
    - If only one argument is specified, then `min` is set to 0 and the argument is used as `max`.
    - If no arguments are specified, then the function returns either `0` or `1`.

### Mathematics:
Basic math functions:

- `number min(number value1, number value2)`
    - Returns the smallest of the two given values.
- `number max(number value1, number value2)`
    - Returns the biggest of the two given values.
- `number clamp(number value, number min, number max)`
    - Returns the given value if it is inside the min-max range. Otherwise, returns min (if the given value is too small) or max (if the given value is too large). 
- `number abs(number value)`
    - Returns the absolute value of the given value.
- `number round(number value)`
    - Returns the given value rounded to the nearest integer value.
- `number floor(number value)`
    - Returns the given value rounded down to the nearest integer value.
- `number ceil(number value)`
    - Returns the given value rounded up to the nearest integer value.
- `number pow(number value, number power)`
    - Raises the given value to the given power.
- `number sqrt(number value)`
    - Takes the square root of the given value.

### Trigonometry:
Functions related to rotations and angles:

- `number sin(number radians)`
    - Returns the sine of the given angle.
- `number cos(number radians)`
    - Returns the cosine of the given angle.
- `number tan(number radians)`
    - Returns the tangent of the given angle.
- `number asin(number sine)`
    - Returns the angle (in radians) whose sine is the given value.
- `number acos(number cosine)`
    - Returns the angle (in radians) whose cosine is the given value.
- `number atan(number tangent)`
    - Returns the angle (in radians) whose tangent is the given value.
- `number atan2(number y, number x)`
    - Returns the angle (in radians) whose tangent is the quotient of the given values.
- `number deg2rad(number degrees)`
    - Converts degrees (0 - 360°) to radians (0 - 2π).
- `number rad2deg(number radians)`
    - Converts radians (0 - 2π) to degrees (0 - 360°).

### Colors:
- `vector color(vector color)`
    - Returns a valid color vector, where each value is rounded and the first 3 values are clamped to the 0-255 range. If the given vector is too short, it will be padded with 0's until it contains 3 values. If the given vector is too long, only the first 4 values will be used.

### Flags:
Some entities use flags - various options that can be enabled or disabled. All of these options are stored together in a single number. Note that flag numbers start at 0, so `hasflag(0, flags)` checks whether the first flag is enabled.

- `none|number hasflag(number flag, number? flags)`
    - Checks whether the specified flag is enabled in the given flags value. Returns either `none` (false) or `1` (true).
    - If the `flags` argument is omitted, then the value of the `spawnflags` attribute of the parent entity is used as `flags`.
- `number setflag(number flag, number? set, number? flags)`
    - Returns the given flags value, but with the specified flag enabled or disabled. If `set` is `0`, the flag will be disabled.
    - If the `flags` argument is omitted, then the value of the `spawnflags` attribute of the parent entity is used as `flags`.
    - If the `set` argument is omitted, then it is set to 1 (which will enable the flag).

### Globals:
Global variables can be used to 'communicate' between instances. This is useful when templates contain parts that should be shared across instances - think of multiple buttons that target the same `game_counter` or `multisource`. Global state can make things more difficult to manage however, so try not to overuse these functions.

- `any getglobal(string name)`
    - Returns the value of the specified global variable. Returns `none` if the variable doesn't exist.
- `any setglobal(string name, any value)`
    - Sets the value of the specified global variable, creating it if it doesn't exist yet. Returns the given value.
- `none|number useglobal(string name)`
    - A convenience function that returns `none` the first time it is called, and `1` on any subsequent call (when given the same name). Shorthand for `getglobal(name) or not setglobal(name, 1)`.

### Directories:
The following functions are only available in entity rewrite rules. They are useful for locating template maps when rewriting custom entities to macro entities that must reference specific templates.

It is recommended to store all template maps relative to a specific directory, and to pass that directory to MESS with the `-dir` command-line argument. Template maps can then be referenced like `{dir()}\my_template_map.rmf`.

- `string dir()`
    - Returns the directory that was specified with the `-dir` command-line argument. If MESS was started without a `-dir` argument, then this function returns the directory that the main map is located in.
- `string messdir()`
    - Returns the directory that MESS.exe is located in.