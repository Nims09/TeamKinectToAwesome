//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System;
    using System.Media;
    using System.Globalization;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;


        private Joint lastHandPos;

        private Rect[,] grid;

        private SoundPlayer[,,] simpleSoundPlayers;

        private SolidColorBrush[,] brushes;

        private int instrumentNo;



        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            lastHandPos = new Joint();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            PrepareSoundStrings();
            PrepareGridBrushes();

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }
        }

        private void PrepareSoundStrings()
        {
            simpleSoundPlayers = new SoundPlayer[3, 5, 3];
            string s = Directory.GetCurrentDirectory();
            DirectoryInfo di = new DirectoryInfo(s + "\\..\\..\\" + @"\SoundClips\");
            int instrument = 0;

            foreach(DirectoryInfo sdi in di.GetDirectories())
            {
                if (instrument > 2) break;

                FileInfo[] fi = sdi.GetFiles();

                for (int i = 0; i < 5; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        //Console.WriteLine(s + "\\..\\..\\" + @"\SoundClips\" + sdi.Name + "\\" + fi[i * 4 + j].Name);
                        //Console.WriteLine(s + "\\..\\..\\" + @"\SoundClips\drum\" + fi[i * 4 + j].Name);
                        simpleSoundPlayers[instrument, i,j] = new SoundPlayer(s + "\\..\\..\\" + @"\SoundClips\" + sdi.Name + "\\" + fi[i * 3 + j].Name);
                    }
                }
                instrument++;
                Console.WriteLine();
            }
        }

        private void PrepareGridBrushes()
        {
            this.brushes = new SolidColorBrush[5,4] {{Brushes.PaleGreen, Brushes.PaleTurquoise, Brushes.LightPink, Brushes.LightSteelBlue},
                        {Brushes.LightCoral, Brushes.Khaki, Brushes.LightSkyBlue, Brushes.PaleGreen},
                        {Brushes.PaleTurquoise, Brushes.PaleGreen, Brushes.LightSteelBlue, Brushes.LightSalmon},
                        {Brushes.Khaki, Brushes.Plum, Brushes.LightPink, Brushes.LightBlue},
                        {Brushes.LightCoral, Brushes.PaleTurquoise, Brushes.Khaki, Brushes.PaleGreen}};
        }
    

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                
                #region gridDraw
                // Draw a transparent background to set the render size
                grid = new Rect[5,4];
                //dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));
                for(int i = 0; i < 5; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        grid[i, j] = new Rect(RenderWidth * (i) / 5, RenderHeight * (j) / 4, RenderWidth * (i + 1) / 5, RenderHeight * (j + 1) / 4);
                        dc.DrawRectangle(brushes[i,j], null, grid[i,j]);
                        if (j == 3)
                        {
                            switch (i)
                            {
                                case 1:
                                    dc.DrawText(new FormattedText("Drum",
                                              CultureInfo.GetCultureInfo("en-us"),
                                              FlowDirection.LeftToRight,
                                              new Typeface("Verdana"),
                                              36, System.Windows.Media.Brushes.White),
                                              new System.Windows.Point(RenderWidth * (i) / 5, RenderHeight * (j) / 4 + 80));
                                    break;
                                case 2:
                                    dc.DrawText(new FormattedText("Piano",
                                              CultureInfo.GetCultureInfo("en-us"),
                                              FlowDirection.LeftToRight,
                                              new Typeface("Verdana"),
                                              36, System.Windows.Media.Brushes.White),
                                              new System.Windows.Point(RenderWidth * (i) / 5, RenderHeight * (j) / 4 + 80));
                                    break;
                                case 3:
                                    dc.DrawText(new FormattedText("Sax",
                                              CultureInfo.GetCultureInfo("en-us"),
                                              FlowDirection.LeftToRight,
                                              new Typeface("Verdana"),
                                              36, System.Windows.Media.Brushes.White),
                                              new System.Windows.Point(RenderWidth * (i) / 5, RenderHeight * (j) / 4 + 80));
                                    break;
                                default:
                                    break;
                            }

                        }
                    }
                }
                Pen p = new Pen();
                p.Brush = Brushes.White;
                for (int i = 1; i < 5; i++)
                { 
                    dc.DrawLine(p, new Point(RenderWidth*i / 5, 0.0), new Point(RenderWidth*i / 5, RenderHeight));
                }
                for (int i = 1; i < 4; i++)
                {
                    dc.DrawLine(p, new Point(0.0, RenderHeight*i/4), new Point(RenderWidth, RenderHeight*i/4));
                }
                #endregion gridDraw
                

                if (skeletons.Length != 0)
                    {
                        foreach (Skeleton skel in skeletons)
                        {
                            RenderClippedEdges(skel, dc);

                            if (skel.TrackingState == SkeletonTrackingState.Tracked)
                            {
                                this.DrawBonesAndJoints(skel, dc);
                            }
                            else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                            {
                                dc.DrawEllipse(
                                this.centerPointBrush,
                                null,
                                this.SkeletonPointToScreen(skel.Position),
                                BodyCenterThickness,
                                BodyCenterThickness);
                            }
                        }
                    }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            double threshold = 0.05;
            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;                    
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (joint.JointType == JointType.HandRight || joint.JointType == JointType.HandLeft)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                    if (Math.Abs(lastHandPos.Position.Z) - Math.Abs(joint.Position.Z) > threshold)
                    {
                        Point lastScreenPosition = SkeletonPointToScreen(lastHandPos.Position);

                        statusBar.Background = Brushes.Green;
                        string s = Directory.GetCurrentDirectory();
                        Console.Write(s);

                        Point yo = SkeletonPointToScreen(joint.Position);
                       
                        Point p = ChooseQuadrant(yo);
                        if(p.Y == 3)
                        {
                            DateTime instrumentSetTime;
                            if (p.X < 4 && p.X > 0) 
                            {
                                this.instrumentNo = (int)(p.X - 1);
                                instrumentSetTime = DateTime.Now;
                            }

                            if (DateTime.Compare(instrumentSetTime.AddSeconds(1), DateTime.Now) > 0)
                            {
                                switch (this.instrumentNo)
                                {
                                    case 0:
                                        titletext.Text = "Drumkit";
                                        break;
                                    case 1:
                                        titletext.Text = "Piano";
                                        break;
                                    default:
                                        titletext.Text = "Saxophone";
                                        break;
                                }
                            }      
                        }
                        else
                        {
                            simpleSoundPlayers[instrumentNo, (int)p.X, (int)p.Y].PlaySync();
                        }
                    }
                    else
                    {
                        statusBar.Background = Brushes.Red;
                    }

                }
                lastHandPos = joint;
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }
        
        private Point ChooseQuadrant(Point P)
        {
            int posX;
            int posY;

            if (P.X < RenderWidth/5)                posX = 0;
            else if (P.X < 2*RenderWidth/5)         posX = 1;
            else if (P.X < 3 * RenderWidth / 5)     posX = 2;
            else if (P.X < 4 * RenderWidth / 5)     posX = 3;
            else                                    posX = 4;

            if (P.Y < RenderHeight/4)               posY = 0;
            else if (P.Y < 2 * RenderHeight/4)      posY = 1;
            else if (P.Y < 3 * RenderHeight / 4)    posY = 2;
            else                                    posY = 3;
            
            return new Point(posX, posY);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }
    }
}