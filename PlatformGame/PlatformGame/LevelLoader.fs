[<AutoOpen>]
module LevelLoad

open LevelDSL

open System

open MonoTouch
open MonoTouch.AVFoundation
open MonoTouch.CoreGraphics
open MonoTouch.Foundation
open MonoTouch.SpriteKit

//Open an mp3, play it and return an IDisposable to stop and cleanup.
let playSong song = 
    let mutable error : NSError = null
    let audioplayer = new AVAudioPlayer(NSUrl.FromFilename song, "mp3", &error)
    ignore <| audioplayer.Retain() //Without this line the audio immediately stops
    audioplayer.NumberOfLoops <- -1 //Loop forever
    ignore <| audioplayer.Play()
    { new IDisposable with
        member __.Dispose() =  
            audioplayer.Stop()
            audioplayer.Release()
            audioplayer.Dispose()}

let makePhysicsBody (sprite:SKSpriteNode) = function
| Circle -> SKPhysicsBody.BodyWithCircleOfRadius (sprite.Size.Height / 2.0f)
| Rectangle -> SKPhysicsBody.BodyWithRectangleOfSize sprite.Size

let makePath = function
| Ellipse(x,y,w,h) -> CGPath.EllipseFromRect(System.Drawing.RectangleF(float32 x, float32 y, float32 w, float32 h), CGAffineTransform.MakeIdentity())

let rec loadSprite (atlas:SKTextureAtlas) = function
| Sprite(name, location, physics, children) -> 
    let spriteNode = new SKSpriteNode(atlas.TextureNamed name)
    match location with
    | Point(x,y) -> 
        spriteNode.Position <- System.Drawing.PointF(float32 x, float32 y)
    | Path(path, time, repeat) -> 
        let action = SKAction.FollowPath(makePath path,false,false, time)
        let action = match repeat with
                     | Once -> action
                     | Times(n) -> SKAction.RepeatAction(action, uint32 n)
                     | Forever -> action|>SKAction.RepeatActionForever
        spriteNode.RunAction action
    
    match physics with
    | NoPhysics -> () // no-op
    | Static(shape) -> 
        spriteNode.PhysicsBody <- makePhysicsBody spriteNode shape
        spriteNode.PhysicsBody.Dynamic <- false
        spriteNode.PhysicsBody.AffectedByGravity <- false
        spriteNode.PhysicsBody.Restitution <- 0.f
    | Dynamic(shape) -> 
        spriteNode.PhysicsBody <- makePhysicsBody spriteNode shape
        spriteNode.PhysicsBody.Dynamic <- true
        spriteNode.PhysicsBody.AffectedByGravity <- true
    | DynamicNoGravity(shape) -> 
        spriteNode.PhysicsBody <- makePhysicsBody spriteNode shape
        spriteNode.PhysicsBody.Dynamic <- true
        spriteNode.PhysicsBody.AffectedByGravity <- false
    children|>List.map(fun s-> loadSprite atlas s) |>List.iter spriteNode.AddChild
    spriteNode  

let LoadLevel (level:Level) (scrollNode: SKNode) (backgroundNode:SKNode) = 
    let musicDisposable = playSong (level.Name+".mp3")
    let atlas = SKTextureAtlas.FromName level.Name

    level.Level|>List.map(fun s-> loadSprite atlas s)|>List.iter scrollNode.AddChild
    level.Background|>List.map(fun s-> loadSprite atlas s)|>List.iter backgroundNode.AddChild
    
    musicDisposable
    