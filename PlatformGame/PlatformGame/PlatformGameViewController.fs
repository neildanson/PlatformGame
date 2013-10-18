namespace PlatformGame

open System
open System.Drawing

open MonoTouch.AVFoundation
open MonoTouch.CoreGraphics
open MonoTouch.Foundation
open MonoTouch.UIKit

open MonoTouch.SpriteKit

type LevelComplete = | Died | Continue

type Scene(size:SizeF) = 
    inherit SKScene(size) 
        
    member val UpdateEvent = Event<_>()
    member val DidSimulatePhysicsEvent = Event<_>()
    override s.Update time = 
       s.UpdateEvent.Trigger time
        
    override s.DidSimulatePhysics() = 
        s.DidSimulatePhysicsEvent.Trigger None

[<Register ("PlatformGameViewController")>]
type PlatformGameViewController () =
    inherit UIViewController ()

    override x.DidReceiveMemoryWarning () =
        // Releases the view if it doesn't have a superview.
        base.DidReceiveMemoryWarning ()
        // Release any cached data, images, etc that aren't in use.

    override x.ViewDidLoad () =
        base.ViewDidLoad ()
        
        //Create SpriteKit view
        let skView = new SKView()
        skView.Bounds <- RectangleF(0.f,0.f, 
                                    x.View.Bounds.Width * UIScreen.MainScreen.Scale, 
                                    x.View.Bounds.Height * UIScreen.MainScreen.Scale)
        #if DEBUG
        skView.ShowsDrawCount <- true
        skView.ShowsFPS <- true
        skView.ShowsNodeCount <- true
        #endif
        x.View <- skView

        //Create a SpriteKit Scene - 640x480 is high enough to look OK
        // & low enough to not require an artist
        let scene =  new Scene(new SizeF(640.f, 480.f), BackgroundColor=UIColor.Blue)
        scene.ScaleMode <- SKSceneScaleMode.AspectFit
        
        
        
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
        
        //Simple start screen - 
        //Setup a tap recognizer that broadcasts an event
        //Which we await
        let startScreen() = async {
            use introMusic = playSong "IntroMusic.mp3"
            let tapEvent = Event<_>()
            let tapRecognizer = new UITapGestureRecognizer(Action<_>(fun x -> tapEvent.Trigger(null)))
            x.View.AddGestureRecognizer tapRecognizer
            
            //Add some text to the screen
            use tapToBegin = new SKLabelNode("Papyrus", Text="Tap to Begin", FontSize=42.f)
            tapToBegin.Position <- PointF(320.f, 240.f)
            scene.AddChild tapToBegin
        
            //Give a little animation
            let scale = SKAction.Sequence [| SKAction.ScaleTo(1.5f,0.5)
                                             SKAction.ScaleTo(1.0f,0.5) |]
                        |> SKAction.RepeatActionForever                     
            tapToBegin.RunAction(scale)
            
            //Wait for the tap
            let! _ = Async.AwaitEvent tapEvent.Publish
            
            //Cleanup!!
            scene.RemoveAllChildren()
            x.View.RemoveGestureRecognizer tapRecognizer
        }
        
        
        //Simple Game over screen - 
        //Show the obligatory failure text and wait 5 seconds
        let gameOver() = async {      
            use gameOverMusic = playSong "GameOver.mp3"      
            //Add some text to the screen
            use gameOverText = new SKLabelNode("Papyrus", Text="Game Over", FontSize=42.f, Scale=10.f)
            gameOverText.Position <- PointF(320.f, 240.f)
            scene.AddChild gameOverText  
            
            //Give a little animation
            let action = SKAction.Group [| SKAction.ScaleTo(1.0f,2.) 
                                           SKAction.RotateToAngle(float32 Math.PI * 2.f, 1.9)|] 
            gameOverText.RunAction(action) 
            
            //Wait 5 seconds, to rub in the player's abject failure             
            do! Async.Sleep 5000

            //Cleanup!!
            scene.RemoveAllChildren()
        }
        
        let success() = async {
            let loadParticles res = 
                let emitterpath = NSBundle.MainBundle.PathForResource (res, "sks")
                NSKeyedUnarchiver.UnarchiveFile(emitterpath) :?> SKEmitterNode
            
            use sparks = loadParticles "Explosion"
            sparks.Position <- PointF(320.f, 240.f)
            
            scene.AddChild sparks
            
            do! Async.Sleep 3000
            //Cleanup!!
            scene.RemoveAllChildren()
        }
        
        let level1() = async {   
            // BEGIN LEVEL SETUP ------------------------------------------------------------------------------------
            use song = playSong "Level1.mp3"
            
            use scrollNode = new SKNode()    
            scene.AddChild scrollNode
            use parallaxScrollNode = new SKNode()
            parallaxScrollNode.ZPosition <- -1.f
            scene.AddChild parallaxScrollNode
            
            let createLevelSprite (name:string) = 
                let sprite = new SKSpriteNode(name)
                sprite.PhysicsBody <- SKPhysicsBody.BodyWithRectangleOfSize sprite.Size
                sprite.PhysicsBody.AffectedByGravity <- false
                sprite.PhysicsBody.Dynamic <- false
                sprite
                
                
            //Pop in some floor
            for i in 0..100 do 
                use grass = createLevelSprite "grass" 
                grass.Position <- PointF(float32 i * grass.Size.Width, 0.f)
                scrollNode.AddChild grass
            
            for i in 104..200 do 
                use grass = createLevelSprite "grass" 
                grass.Position <- PointF(float32 i * grass.Size.Width, 0.f)
                scrollNode.AddChild grass
            
            //Pop in a parallax layer
            for i in 0..50 do 
                use hill = new SKSpriteNode "hill_small"
                hill.Position <- PointF(float32 i * 70.f, 50.f)
                parallaxScrollNode.AddChild hill 
            
            //Lets create a moving platform from 3 connected sprites
            use platformLeft = createLevelSprite "stoneLeft"
            use platformCenter = createLevelSprite "stoneMid"
            use platformRight = createLevelSprite "stoneRight"
            //Position the left and right **relative** to the center one
            platformLeft.Position <- PointF(-platformLeft.Size.Width, 0.0f)
            platformRight.Position <- PointF(platformRight.Size.Width, 0.0f)
            //Add them as children of the center sprite
            platformCenter.AddChild platformLeft
            platformCenter.AddChild platformRight
            
            //END LEVEL SETUP ---------------------------------------------------------------------------------------
            
            //BEGIN PLAYER SETUP ------------------------------------------------------------------------------------
            
            //Add the center sprite to the scene (this adds all 3)
            scrollNode.AddChild platformCenter
            //Next lets define a path that the platform will follow - like Super Mario World 
            let path = CGPath.EllipseFromRect(RectangleF(50.f,150.f,200.f,200.f), CGAffineTransform.MakeIdentity())
            let movePlatform = SKAction.FollowPath(path, false, false, 5.0)|>SKAction.RepeatActionForever
            platformCenter.RunAction movePlatform
            
            let playerAtlas = SKTextureAtlas.FromName "Player"
            let playerAnimationTextures = playerAtlas.TextureNames.[1..playerAtlas.TextureNames.Length-1]
                                          |>Array.sort
                                          |>Array.map playerAtlas.TextureNamed
            
            //Add a player, with physics which are affected by gravity and are dynamic
            let levelCompleteEvt = Event<_>()
            
            use player = new SKSpriteNode(playerAnimationTextures.[0])
            player.PhysicsBody <- SKPhysicsBody.BodyWithCircleOfRadius (player.Size.Height / 2.f)
            player.PhysicsBody.AffectedByGravity <- true
            player.PhysicsBody.AllowsRotation <- false
            player.PhysicsBody.Dynamic <- true
            player.PhysicsBody.UsesPreciseCollisionDetection <- true
            player.Position <- PointF(320.f,100.f)
            player.Name <- "Player"
            scrollNode.AddChild player
            
            //Play our animated frames - note the number is the time of EACH FRAME
            let animation = SKAction.AnimateWithTextures(playerAnimationTextures, 0.05) 
                            |> SKAction.RepeatActionForever
            player.RunAction animation
            
            use jumpSound = SKAction.PlaySoundFileNamed("Jump.wav", true)
            
            //Add a swipe up to jump. 
            let swipeUp = new UISwipeGestureRecognizer(Direction=UISwipeGestureRecognizerDirection.Up)
            swipeUp.AddTarget (fun () -> 
                scene.RunAction jumpSound
                if swipeUp.State = UIGestureRecognizerState.Ended && player.PhysicsBody.Velocity.dy = 0.f then
                    player.PhysicsBody.ApplyImpulse (CGVector(0.0f, 200.f))) |> ignore
            //Add event to know when Update is called
            use _ = scene.UpdateEvent.Publish.Subscribe(fun time -> player.PhysicsBody.ApplyImpulse(CGVector(2.0f,0.0f)))
            //Move the scroll node inversely to the players position so the player stays centered on screen
            use _ = scene.DidSimulatePhysicsEvent.Publish.Subscribe(fun _ -> 
                scrollNode.Position <- PointF(320.f - player.Position.X,0.0f)
                parallaxScrollNode.Position <- PointF(-player.Position.X / 2.f, 0.f)
                if player.Position.X > 10000.f then 
                    levelCompleteEvt.Trigger Continue
                if player.Position.Y < -100.f then
                    levelCompleteEvt.Trigger Died
                )
            x.View.AddGestureRecognizer swipeUp
            
            // END PLAYER SETUP ---------------------------------------------------------------------------------------
            
            let! levelComplete = Async.AwaitEvent levelCompleteEvt.Publish
            
            x.View.RemoveGestureRecognizer swipeUp
            return levelComplete
            //Note: we dont clear the scene right now as we go to GameOver and it looks cool
            //If we leave the level in the background during game over :)
        }

        //Define the loop
        let rec gameLoop() = async {
            do! startScreen()
            let! levelEnd = level1()
            match levelEnd with
            | Continue -> do! success()
            | Died -> do! gameOver()
            return! gameLoop()
        } 
        
        //Run the loop
        gameLoop() |> Async.StartImmediate
                
        //Present the scene
        skView.PresentScene scene
    

    override x.ShouldAutorotateToInterfaceOrientation (toInterfaceOrientation) =
        // Return true for supported orientations
        if UIDevice.CurrentDevice.UserInterfaceIdiom = UIUserInterfaceIdiom.Phone then
           toInterfaceOrientation <> UIInterfaceOrientation.PortraitUpsideDown
        else
           true


