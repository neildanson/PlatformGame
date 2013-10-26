module Level1

open MonoTouch.CoreGraphics
open System.Drawing
open LevelDSL

let steps xpos = 
    [ for x in 1..3 do
        for y in 1..x do
            yield Sprite("grass", Point((x+xpos)*70, y*70), Static(Rectangle), []) ]

let platform startx = [ for i in startx..startx+100 -> Sprite("grass", Point(i * 70, 0), Static Rectangle, []) ]

let movingEllipsePlatform x y = 
    Sprite("stoneMid", 
           Path(Ellipse(x,y,200,200), 3.0, Forever), 
           Static(Rectangle),
           [ Sprite("stoneLeft", Point(-70,0), Static(Rectangle), [])
             Sprite("stoneRight", Point(70,0), Static(Rectangle), [])])

let level1 = {
    Name = "Level1"
    Level = [ yield! platform 0
              yield! platform 104
              yield! steps 20
              yield! steps 97
              yield! steps 110
              yield movingEllipsePlatform 50 150
              yield movingEllipsePlatform 1000 160
              yield movingEllipsePlatform 3000 150
            ]
    
    Background = [ for i in 0..150 -> Sprite("hill_small", Point(i * 70, 85), NoPhysics, []) ] @
                 [ for i in 0..100 -> Sprite("cloud", Point(i * 300, 400), NoPhysics, []) ]
}