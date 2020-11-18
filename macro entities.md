# MESS - Entity guide

The entities provided by MESS are called macro entities. They're not part of the final map file, but are expanded by MESS into multiple brushes and entities.


## Table of contents

- [Template entities](#template-entities)
    - [macro\_template](#macro\_template)
    - [macro\_remove\_if](#macro\_remove\_if)
- [Instance entities](#instance-entities)
    - [macro\_insert](#macro\_insert)
    - [macro\_cover](#macro\_cover)
    - [macro\_fill](#macro\_fill)
- [Other entities](#other-entities)
    - [macro\_brush](#macro\_brush)

## Template entities

### macro\_template
Anything inside the bounding box of this entity becomes part of a template. Templates are removed from the map, but instances can then be created in various other places by other macro entities. For templates that are useful across a wide variety of maps it's better to create a separate template map.

#### Attributes
- **Name** *(targetname)* - The name of this template. If multiple templates have the same name, one of them is chosen randomly.
- **Anchor point (origin)** *(anchor)* - The origin of this template.
    - 0 = Bottom center
    - 1 = Center
    - 2 = Top center
    - 3 = Origin brush
- **Random selection weight** *(selection\_weight)* - This determines how likely this template is to be chosen when there are multiple templates with the same name. If this is set to 0 then the template will not be chosen at all, even if there are no other templates with the same name.

#### Notes
- Because it's the bounding box that counts, it's a good idea to mark template areas with 2 brushes rather than 1: one on the left and one on the right. This makes it easier to select and modify the template contents.
- Templates that contain `macro_insert` or other instance-creating macro entities are called 'recursive templates'. This can be used to create a specific number of instances.

---

### macro\_remove\_if
Used inside templates. When an instance of a template is created, anything inside the bounding box of this entity is excluded from that instance if the removal condition is true.

#### Attributes
- **Removal condition** *(condition)* - The condition that determines whether the contents of this entity must be excluded. `none` (empty) and `0` will prevent removal.

## Instance entities

### macro\_insert
This point entity creates a single template instance. The attributes of this entity are visible to script expressions in the selected template, so each instance can be made unique. Useful for duplicating complex entity setups.

#### Attributes
- **Name** *(targetname)* - The name of this entity. Templates can reference this in expressions with `{targetname}` or by using the special function `{id()}`, which returns either the name of the instance-creating entity of the unique ID of the current instance.
- **Template map path** *(template\_map)* - The relative or absolute path of a map file. A template map does not need to contain `macro_template` areas - the entire map acts as a template. This attribute takes precedence over `template_name`.
- **Template entity** *(template\_name)* - The name of a `macro_template` area in the current map.
- **Angles (Pitch Yaw Roll)** *(angles)* - The orientation of the instance. This also affects the `angles` attribute of entities within the selected template.
- **Scale** *(scale)* - The scale of the instance. This also affects the `scale` attribute of entities within the selected template.

---

### macro\_cover
A brush entity that creates multiple template instances across its non-NULL faces. Useful for covering terrain with props.

#### Attributes
- **Name** *(targetname)* - The name of this entity. Because this entity can create multiple instances, it's better to use the unique instance ID in templates (with `{iid()}`), unless every instance really does need to use the same entity name.
- **Template map path** *(template\_map)* - The relative or absolute path of a map file.
- **Template entity** *(template\_name)* - The name of a `macro_template` area in the current map.
- **Maximum number of instances** *(max\_instances)* - The maximum number of instances to create. Between 0 and 1, this acts as a 'coverage factor', which uses the total surface area of all non-NULL faces and the surface area of each instance to determine how many instances to create. Above 1, this is the actual maximum number of instances that will be created.
- **Instance radius** *(radius)* - How much space to leave empty around the center of each instance. This can be used to prevent instances from overlapping each other. Setting this to 0 disables overlap prevention.
- **Random seed** *(random\_seed)* - MESS uses a pseudo-random number generator to determine the position of each instance. The same seed will produce the same positions, as long as the position, shape and texturing of this entity is not changed.
- **Instance orientation** *(instance\_orientation)* - The relative orientation of each instance.
    - 0 = Global
    - 1 = Local
    - 2 = Face
    - 3 = Texture plane
- **Instance angles (Pitch Yaw Roll)** *(instance\_angles)* - The orientation of an instance. This attribute is evaluated again for each instance, so a `rand` or `randi` function call can produce a different value for each instance.
- **Instance scale** *(instance\_scale)* - The scale of an instance. This attribute is also evaluated again for each instance.
- **Brush behavior** *(brush\_behavior)* - What to do with the brushwork of the `macro_cover` entity:
    - 0 = Remove brushes
    - 1 = Leave as world geometry
    - 2 = Leave as func\_detail

#### Notes
- As with `macro_insert`, custom attributes are visible to expressions in the chosen template. And just as the `instance_angles` and `instance_scale` attributes, custom attributes are evaluated again for each instance.
- If you want to turn the brushwork of a `macro_cover` entity into an entity with specific settings, then create a template that contains a `macro_cover` and the desired entity, and change the original `macro_cover` entity to a `macro_brush`.

---

### macro\_fill
A brush entity that creates multiple template instances inside its brushes. Useful to fill an area with particles or other things.

#### Attributes
- **Name** *(targetname)* - The name of this entity. Because this entity can create multiple instances, it's better to use the unique instance ID in templates (with `{iid()}`), unless every instance really does need to use the same entity name.
- **Template map path** *(template\_map)* - The relative or absolute path of a map file.
- **Template entity** *(template\_name)* - The name of a `macro_template` area in the current map.
- **Maximum number of instances** *(max\_instances)* - The maximum number of instances to create. Between 0 and 1, this acts as a 'coverage factor', which uses the total volume and the volume of each instance to determine how many instances to create. Above 1, this is the actual maximum number of instances that will be created.
- **Instance radius** *(radius)* - How much space to leave empty around the center of each instance. This can be used to prevent instances from overlapping each other. Setting this to 0 disables overlap prevention.
- **Random seed** *(random\_seed)* - MESS uses a pseudo-random number generator to determine the position of each instance. The same seed will produce the same positions, as long as the position and shape of this entity is not changed.
- **Instance orientation** *(instance\_orientation)* - The relative orientation of each instance.
    - 0 = Global
    - 1 = Local
- **Instance angles (Pitch Yaw Roll)** *(instance\_angles)* - The orientation of an instance. This attribute is evaluated again for each instance, so a `rand` or `randi` function call can produce a different value for each instance.
- **Instance scale** *(instance\_scale)* - The scale of an instance. This attribute is also evaluated again for each instance.
- **Fill mode** *(fill\_mode)* - Determines how instance positions are determined.
	- 0 = Random points
	- 1 = Random grid points
	- 2 = All grid points
- **Grid orientation** *(grid\_orientation)* - Determines the orientation of the grid, for the 'Random grid points' and 'All grid points' modes.  
	- 0 = Global
	- 1 = Local
- **Grid granularity** *(grid\_granularity)* - The granularity of the grid that instance positions are snapped to. In 'Random grid points' mode, setting an axis to 0 disables snapping along that axis, so for example `4 0 0` will only snap along the x-axis. In 'All grid points' mode, each axis has a minimum granularity of 1.

#### Notes
- The origin of the snapping grid can be set with an ORIGIN brush. Otherwise, the origin of the current map (or template, if the `macro_fill` is part of a template) is used.

## Other entities

### macro\_brush
A brush entity that creates a copy of its own brushwork for each world brush and brush entity in the selected template. Each copy takes on the textures and entity attributes of the associated brush or entity in the template. Useful for creating fences, or for adding visual effects to triggers.

#### Attributes
- **Name** *(targetname)* - The name of this entity.
- **Template map path** *(template\_map)* - The relative or absolute path of a map file. A template map does not need to contain `macro_template` areas - the entire map acts as a template. This attribute takes precedence over `template_name`.
- **Template entity** *(template\_name)* - The name of a `macro_template` area in the current map.

#### Notes
- Brushwork copies will use the original `macro_brush` textures if the template brush or entity is covered with the NULL texture. Otherwise they take on the texture of the template brush or entity.
- World brushes in the selected template are skipped if they're covered with multiple textures.
- Point entities in the selected template are ignored. This includes `macro_insert`.  