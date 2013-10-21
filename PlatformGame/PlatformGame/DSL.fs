[<AutoOpen>]
module LevelDSL

open MonoTouch
open MonoTouch.CoreGraphics
open MonoTouch.SpriteKit

// Repeat action 1 time, n times or infinitely
type RepeatAction = Once | Times of int | Forever
type Seconds = float
type TextureName = string

type Path = 
| Ellipse of int * int * int * int //Bounding rectangle
// TODO Other path types

// A thing can either be at a single point or follow a path over time (n times)
type Location = 
| Point of int * int
| Path of Path * Seconds * RepeatAction 

//The Shape used for Physics
type Shape =
| Rectangle 
| Circle

//Details about the Physics
type Physics = 
| NoPhysics
| Static of Shape
| Dynamic of Shape
| DynamicNoGravity of Shape

//Lightweight sprite definition
type Sprite =
| Sprite of TextureName * Location * Physics * Sprite list

type Level = {
    Name : string
    Level : Sprite list
    Background : Sprite list }


