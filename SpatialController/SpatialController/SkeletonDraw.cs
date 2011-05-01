/**
 * This class is borrowed from the TrackingNI project by
 * Richard Pianka and Abouza.
 **/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using xn;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Drawing.Drawing2D;
using System.Drawing;

namespace TrackingNI
{
    public class SkeletonDraw
    {
        private DepthGenerator depthGenerator;

        public void DrawStickFigure(ref WriteableBitmap image, DepthGenerator depthGenerator, DepthMetaData data, UserGenerator userGenerator)
        {
            Point3D corner = new Point3D(data.XRes, data.YRes, data.ZRes);
            corner = depthGenerator.ConvertProjectiveToRealWorld(corner);
            this.depthGenerator = depthGenerator;

            int nXRes = data.XRes;
            int nYRes = data.YRes;

            uint[] users = userGenerator.GetUsers();
            foreach (uint user in users)
            {
                if (userGenerator.GetSkeletonCap().IsTracking(user))
                {
                    DrawSingleUser(ref image, user, userGenerator, corner);
                }
            }
        }

        public void DrawSingleUser(ref WriteableBitmap image, uint id, UserGenerator userGenerator, Point3D corner)
        {
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.LeftHand, SkeletonJoint.LeftElbow, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.LeftElbow, SkeletonJoint.LeftShoulder, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.LeftShoulder, SkeletonJoint.Torso, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.LeftShoulder, SkeletonJoint.RightShoulder, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.Torso, SkeletonJoint.RightShoulder, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.RightShoulder, SkeletonJoint.RightElbow, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.RightElbow, SkeletonJoint.RightHand, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.Neck, SkeletonJoint.Head, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.Torso, SkeletonJoint.LeftHip, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.Torso, SkeletonJoint.RightHip, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.LeftHip, SkeletonJoint.RightHip, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.LeftHip, SkeletonJoint.LeftKnee, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.LeftKnee, SkeletonJoint.LeftFoot, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.RightHip, SkeletonJoint.RightKnee, corner);
            DrawStickLine(ref image, id, userGenerator, SkeletonJoint.RightKnee, SkeletonJoint.RightFoot, corner);
            DrawHeadAndHands(ref image, id, userGenerator, depthGenerator);
            

            SkeletonJointPosition leftShoulder = new SkeletonJointPosition();
            SkeletonJointPosition rightShoulder = new SkeletonJointPosition();
            SkeletonJointPosition neck = new SkeletonJointPosition();
            SkeletonJointPosition midShoulder = new SkeletonJointPosition();

            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, SkeletonJoint.LeftShoulder, ref leftShoulder);
            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, SkeletonJoint.RightShoulder, ref rightShoulder);
            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, SkeletonJoint.Neck, ref neck);

            midShoulder.position = new Point3D((leftShoulder.position.X + rightShoulder.position.X) / 2,
                                               (leftShoulder.position.Y + rightShoulder.position.Y) / 2,
                                               (leftShoulder.position.Z + rightShoulder.position.Z) / 2);
            midShoulder.fConfidence = (leftShoulder.fConfidence + rightShoulder.fConfidence) / 2;
        }

        public void DrawStickLine(ref WriteableBitmap image, uint id, UserGenerator userGenerator, SkeletonJoint first, SkeletonJoint second, Point3D corner)
        {
            SkeletonJointPosition a = new SkeletonJointPosition();
            SkeletonJointPosition b = new SkeletonJointPosition();

            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, first, ref a);
            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, second, ref b);

            if (a.fConfidence == 1 && b.fConfidence == 1)
            {
                // choose color
            }
            else
            {
                if ((a.position.X == 0 && a.position.Y == 0 && a.position.Z == 0) ||
                    (b.position.X == 0 && b.position.Y == 0 && b.position.Z == 0))
                {
                    return;
                }
            }

            DrawTheLine(ref image, ref a, ref b);
        }

        public void DrawTheLine(ref WriteableBitmap image, ref SkeletonJointPosition joint1, ref SkeletonJointPosition joint2)
        {
            image.Lock();

            var b = new Bitmap(image.PixelWidth, image.PixelHeight, image.BackBufferStride, System.Drawing.Imaging.PixelFormat.Format24bppRgb,
                image.BackBuffer);

            using (var bitmapGraphics = System.Drawing.Graphics.FromImage(b))
            {
                bitmapGraphics.SmoothingMode = SmoothingMode.HighSpeed;
                bitmapGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                bitmapGraphics.CompositingMode = CompositingMode.SourceCopy;
                bitmapGraphics.CompositingQuality = CompositingQuality.HighSpeed;

                int[] joint1Coord = ConvertCoord(joint1, 0);
                int[] joint2Coord = ConvertCoord(joint2, 0);

                bitmapGraphics.DrawLine(Pens.BlueViolet, joint1Coord[0], joint1Coord[1], joint2Coord[0], joint2Coord[1]);
                bitmapGraphics.Dispose();
            }

            image.AddDirtyRect(new Int32Rect(0, 0, image.PixelWidth, image.PixelHeight));
            image.Unlock();
        }

        public void DrawHeadAndHands(ref WriteableBitmap image, uint id, UserGenerator userGenerator, DepthGenerator depthGenerator) 
        {
            int headSize = 40; int handSize = 20;

            SkeletonJointPosition head = new SkeletonJointPosition();
            SkeletonJointPosition leftHand = new SkeletonJointPosition();
            SkeletonJointPosition rightHand = new SkeletonJointPosition();

            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, SkeletonJoint.Head, ref head);
            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, SkeletonJoint.LeftHand, ref leftHand);
            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, SkeletonJoint.RightHand, ref rightHand);

            image.Lock();

            var b = new Bitmap(image.PixelWidth, image.PixelHeight, image.BackBufferStride, System.Drawing.Imaging.PixelFormat.Format24bppRgb,
                image.BackBuffer);

            using (var bitmapGraphics = System.Drawing.Graphics.FromImage(b))
            {
                bitmapGraphics.SmoothingMode = SmoothingMode.HighSpeed;
                bitmapGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                bitmapGraphics.CompositingMode = CompositingMode.SourceCopy;
                bitmapGraphics.CompositingQuality = CompositingQuality.HighSpeed;

                int[] headCoord = ConvertCoord(head, -headSize/2);
                int[] leftHandCoord = ConvertCoord(leftHand, -handSize/2);
                int[] rightHandCoord = ConvertCoord(rightHand, -handSize/2);
                
                bitmapGraphics.DrawEllipse(Pens.BlueViolet, headCoord[0], headCoord[1], headSize, headSize);
                bitmapGraphics.DrawEllipse(Pens.BlueViolet, leftHandCoord[0], leftHandCoord[1], handSize, handSize);
                bitmapGraphics.DrawEllipse(Pens.BlueViolet, rightHandCoord[0], rightHandCoord[1], handSize, handSize);
                
                bitmapGraphics.Dispose();
            }
            image.AddDirtyRect(new Int32Rect(0, 0, image.PixelWidth, image.PixelHeight));
            image.Unlock();

        }

        public int[] ConvertCoord(SkeletonJointPosition joint, int offset)
        {
            Point3D point = depthGenerator.ConvertRealWorldToProjective(joint.position);
            return new int[] { (point.X >= 0) ? (int)(point.X + offset) : 0, (point.Y >= 0) ? (int)(point.Y + offset) : 0 };
        }

        public void DrawStickPoint(ref WriteableBitmap image, SkeletonJointPosition joint, Point3D corner)
        {
            byte[] point = { 0, 0, 255, 0,
                             0, 0, 255, 0,
                             0, 0, 255, 0,
                             0, 0, 255, 0,
                             0, 0, 255, 0,
                             0, 0, 255, 0,
                             0, 0, 255, 0,
                             0, 0, 255, 0,
                             0, 0, 255, 0, };

            image.Lock();
            image.WritePixels(new Int32Rect(Convert.ToInt32(joint.position.X- 1),
                                            Convert.ToInt32(joint.position.Y - 1),
                                            3, 3), point, 4, 0);
            image.Unlock();
        }

        public void DrawOrientation(ref WriteableBitmap image, uint id, UserGenerator userGenerator, SkeletonJoint joint, Point3D corner)
        {
            SkeletonJointOrientation orientation = new SkeletonJointOrientation();
            SkeletonJointPosition position = new SkeletonJointPosition();

            userGenerator.GetSkeletonCap().GetSkeletonJointPosition(id, joint, ref position);
            userGenerator.GetSkeletonCap().GetSkeletonJointOrientation(id, joint, ref orientation);

            if (position.fConfidence != 1 && orientation.Confidence != 1)
            {
                return;
            }

            SkeletonJointPosition v1 = new SkeletonJointPosition();
            SkeletonJointPosition v2 = new SkeletonJointPosition();
            v1.fConfidence = v2.fConfidence = 1;

            v1.position = position.position;
            v2.position = new Point3D(v1.position.X + 100 * orientation.Orientation.Elements[0],
                                      v1.position.Y + 100 * orientation.Orientation.Elements[3],
                                      v1.position.Z + 100 * orientation.Orientation.Elements[6]);

            DrawTheLine(ref image, ref v1, ref v2);
            
            v1.position = position.position;
            v2.position = new Point3D(v1.position.X + 100 * orientation.Orientation.Elements[1],
                                      v1.position.Y + 100 * orientation.Orientation.Elements[4],
                                      v1.position.Z + 100 * orientation.Orientation.Elements[7]);

            DrawTheLine(ref image, ref v1, ref v2);
            
            v1.position = position.position;
            v2.position = new Point3D(v1.position.X + 100 * orientation.Orientation.Elements[2],
                                      v1.position.Y + 100 * orientation.Orientation.Elements[5],
                                      v1.position.Z + 100 * orientation.Orientation.Elements[8]);
            
            DrawTheLine(ref image, ref v1, ref v2);
            
        }
    }
}
