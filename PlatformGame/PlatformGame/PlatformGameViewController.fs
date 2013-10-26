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
        let scene =  new Scene(new SizeF(640.f, 480.f), BackgroundColor=UIColor.FromRGB(100,100,255))
        scene.ScaleMode <- SKSceneScaleMode.AspectFit
        
        
        
        
        
        //Simple start screen - 
        //Setup a tap recognizer that broadcasts an event
        //Which we await
        let startScreen() = async {
            use introMusic = playSong "IntroMusic.mp3"
            let tapEvent = Event<_>()
            let tapRecognizer = new UITapGestureRecognizer(Action<_>(fun x -> tapEvent.Trigger(null)))
            x.View.AddGestureRecognizer tapRecognizer
            
            use background = new SKSpriteNode("IntroScreen")
            background.Position <- PointF(320.f,240.f)
            scene.AddChild(background)
            
            //Add some text to the screen
            use tapToBegin = new SKLabelNode("Zapfino", Text="Tap to Begin", FontSize=42.f)
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
            use gameOverText = new SKLabelNode("Zapfino", Text="Game Over", FontSize=42.f, Scale=10.f)
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
            use _ = playSong "SuccessScreen.mp3"
            use rocket = loadParticles "Rocket"
            use sparks = loadParticles "Explosion"
            
            rocket.Position <- PointF(320.f,0.f)
            scene.AddChild rocket
            
            do! Async.Sleep 1850
            rocket.RemoveFromParent()
            
            sparks.Position <- PointF(320.f, 240.f)
            
            scene.AddChild sparks
            
            do! Async.Sleep 1800
            //Cleanup!!
            scene.RemoveAllChildren()
        }
        
        let level1() = async {   
            // BEGIN LEVEL SETUP ------------------------------------------------------------------------------------
            use scrollNode = new SKNode()    
            scene.AddChild scrollNode
            use parallaxScrollNode = new SKNode()
            parallaxScrollNode.ZPosition <- -1.f
            scene.AddChild parallaxScrollNode
            
            use level = LoadLevel Level1.level1 scrollNode parallaxScrollNode
            
            //BEGIN PLAYER SETUP ------------------------------------------------------------------------------------
            
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
            player.PhysicsBody.Restitution <- 0.f
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
            let swipeUp = new UITapGestureRecognizer()
            swipeUp.AddTarget (fun () -> 
                scene.RunAction jumpSound
                if player.PhysicsBody.Velocity.dy = 0.f then
                    player.PhysicsBody.ApplyImpulse (CGVector(0.0f, 300.f))) |> ignore
            //Add event to know when Update is called
            use _ = scene.UpdateEvent.Publish.Subscribe(fun time -> player.PhysicsBody.ApplyImpulse(CGVector(3.0f,0.0f)))
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


