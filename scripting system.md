# MESS - Scripting system

MESS contains a small scripting language named MScript. Its syntax is similar to that of Python and Javascript.


## Usage
### Embedded expressions
MScript expressions can be embedded in entity attribute names or values by surrounding them with curly braces. For example, `fire_{4 + 5}` contains the expression `4 + 5`, which evaluates to `9`, so the resulting value will be `fire_9`.

### Parent entities
When a macro entity is instantiating a template, it is seen as the 'parent entity'. Entities within the template are given access to the attributes of this parent entity, so an attribute with the value `fire_{targetname}` is turned into `fire_box1` if the parent entity's `targetname` is `box1`. This works for any attribute, including custom ones. Because MESS only knows about 'internal' attribute names, it's a good idea to turn on `SmartEdit` mode.

The `worldspawn` entity acts as parent entity for anything that is not part of a template. This means that map properties can be used in expressions.


## Common expressions

- If you want to make an attribute customizable, but also provide a default value, use `or`: `{parent_entity_attribute_name or default_value}`.
- If you want to prevent multiple template instances from triggering each other's entities, use the `iid()` function (short for 'instance ID') to ensure instance-specific entity names: `my_entity_{iid()}`.
- Same as the above, but if you want instance-creating entities to be able to set a specific entity name, use the `id()` function instead (`{id()}` is the same as `{targetname or iid()}`).


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

- `pitch` - same as `vector[0]`
- `yaw` - same as `vector[1]`
- `roll` - same as `vector[2]`
- `r` - same as `vector[0]`
- `g` - same as `vector[1]`
- `b` - same as `vector[2]`
- `brightness` - same as `vector[3]`
- `x` - same as `vector[0]`
- `y` - same as `vector[1]`
- `z` - same as `vector[2]`
- `length` - how many numbers a vector contains: `[0, 0, 0].length` produces `3`.


### `string`
A piece of text. Any attribute value that does not look like a number or a sequence of numbers is treated as a string. Within expressions strings must be surrounded by single quotes, to distinguish them from identifiers: `'targetname'` produces the literal text `targetname`, whereas `targetname` would produce the targetname of the macro entity that is instantiating the current template.

Strings can be indexed in much the same way as vectors can, except that indexing returns a 1-character string instead of a number. Negative indexing is also supported, and indexes that do not exist produce the special value `none`.

#### Properties and member functions

- `length` - how many characters a string contains: `'hello'.length` produces `5`.
- `substr(offset, length?)` - this function returns a specific part of a string. Offset can be negative, just as with indexing. Length is optional - if omitted, the rest of the string is returned, starting at offset. Unlike indexing, this returns an empty string if offset and length do not fall within the string boundaries.


### `none`
The special value `none` represents attributes that are empty or missing entirely. Within expressions it is written as `none`.

Unlike many other languages, where working with `null` or `None` often results in errors, `none` typically acts as 0 or a zero-filled vector in arithmetic operations.

In logical contexts, `none` acts as false, while anything that is not `none` acts as true.


## Operations
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

- `a and b` - logical 'and'
- `a or b` - logical 'or'

#### Notes

- Only `none` is seen as false, any other value is seen as true.
- `and` returns the last operand, if both operands are true. Otherwise it returns `none`.
- `or` returns the first operand that is true. If both operands are false it returns `none`.
- `and` will not evaluate the second operand if the first operand is `none`. Likewise, `or` will not evaluate the second operand if the first operand is not `none`.

### Negation
The negation operators are:

- `-a` - numeric negation
- `!a` - logical negation

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


## Functions
Mess also provides the following functions:

- `iid()` - Returns the numeric ID of the current instance.
- `id()` - Returns either the `targetname` of the macro entity that is creating the current instance, or the numeric ID of the current instance. Shorthand for `targetname or iid()`.
- `rand(min?, max?)` - Returns a random number between `min` and `max`. If only one argument is specified, then `min` is set to `0.0` and the argument is used as `max`. If no arguments are specified, then the function returns a random number between `0.0` and `1.0`.
- `randi(min?, max?)` - Returns a random integer number between `min` (inclusive) and `max` (exclusive). If only one argument is specified, then `min` is set to 0 and the argument is used as `max`. If no arguments are specified, then the function returns either `0` or `1`.