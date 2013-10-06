namespace PlatformGame

open System
open System.Drawing

open MonoTouch.Foundation
open MonoTouch.UIKit

open MonoTouch.SpriteKit

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
        let scene =  new SKScene(new SizeF(640.f, 480.f), BackgroundColor=UIColor.Blue)
        scene.ScaleMode <- SKSceneScaleMode.AspectFit
        
        //Simple start screen - 
        //Setup a tap recognizer that broadcasts an event
        //Which we await
        let startScreen() = async {
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
        
        let level1() = async {
            //Probably good to do something more interesting here!!
            do! Async.Sleep 4000
        }

        //Define the loop
        let rec gameLoop() = async {
            do! startScreen()
            do! level1()
            do! gameOver()
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


