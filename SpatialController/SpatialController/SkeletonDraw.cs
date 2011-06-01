/**
 * This class is borrowed from the TrackingNI project by
 * Richard Pianka and Abouza.
 **/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenNI;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Drawing.Drawing2D;
using System.Drawing;

namespace SpatialController
{
    public class SkeletonDraw
    {
        private DepthGenerator depthGenerator;

        public void DrawStickFigure(ref WriteableBitmap image, DepthGenerator depthGenerator, DepthMetaData data,
                UserGenerator userGenerator, Ray3D[] rays)
        {
            Point3D corner = new Point3D(data.XRes, data.YRes, data.ZRes);
            corner = depthGenerator.ConvertProjectiveToRealWorld(corner);
            this.depthGenerator = depthGenerator;

            int nXRes = data.XRes;
            int nYRes = data.YRes;

            // TODO: Fix these.
            /*foreach (Ray3D ray in rays)
            {
                if (ray != null)
                {
                    int[] p0 = ray.point0();
                    int[] p1 = ray.point1();
                    DrawTheLine(ref image, p0, p1);
                }
            }*/

            int[] users = userGenerator.GetUsers();
            foreach (int user in users)
            {
                if (userGenerator.SkeletonCapability.IsTracking(user))
                {
                    DrawSingleUser(ref image, user, userGenerator, corner);
                }
            }
        }

        public void DrawSingleUser(ref WriteableBitmap image, int id, UserGenerator userGenerator, Point3D corner)
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

            leftShoulder = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.LeftShoulder);
            rightShoulder = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.RightShoulder);
            neck = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.Neck);

            midShoulder.Position = new Point3D((leftShoulder.Position.X + rightShoulder.Position.X) / 2,
                                               (leftShoulder.Position.Y + rightShoulder.Position.Y) / 2,
                                               (leftShoulder.Position.Z + rightShoulder.Position.Z) / 2);
            midShoulder.Confidence = (leftShoulder.Confidence + rightShoulder.Confidence) / 2;
        }

        public void DrawStickLine(ref WriteableBitmap image, int id, UserGenerator userGenerator, SkeletonJoint first, SkeletonJoint second, Point3D corner)
        {
            SkeletonJointPosition a = new SkeletonJointPosition();
            SkeletonJointPosition b = new SkeletonJointPosition();

            a = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, first);
            b = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, second);

            if (a.Confidence == 1 && b.Confidence == 1)
            {
                // choose color
            }
            else
            {
                if ((a.Position.X == 0 && a.Position.Y == 0 && a.Position.Z == 0) ||
                    (b.Position.X == 0 && b.Position.Y == 0 && b.Position.Z == 0))
                {
                    return;
                }
            }

            DrawTheLine(ref image, ref a, ref b);
        }

        public void DrawTheLine(ref WriteableBitmap image, ref SkeletonJointPosition joint1, ref SkeletonJointPosition joint2)
        {
            DrawTheLine(ref image, ConvertCoord(joint1, 0), ConvertCoord(joint2, 0));
        }

        public void DrawTheLine(ref WriteableBitmap image, int[] joint1Coord, int[] joint2Coord)
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

                bitmapGraphics.DrawLine(Pens.BlueViolet, joint1Coord[0], joint1Coord[1], joint2Coord[0], joint2Coord[1]);
                bitmapGraphics.Dispose();
            }

            image.AddDirtyRect(new Int32Rect(0, 0, image.PixelWidth, image.PixelHeight));
            image.Unlock();
        }

        public void DrawHeadAndHands(ref WriteableBitmap image, int id, UserGenerator userGenerator, DepthGenerator depthGenerator) 
        {
            int headSize = 40; int handSize = 20;

            SkeletonJointPosition head = new SkeletonJointPosition();
            SkeletonJointPosition leftHand = new SkeletonJointPosition();
            SkeletonJointPosition rightHand = new SkeletonJointPosition();

            head = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.Head);
            leftHand = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.LeftHand);
            rightHand = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, SkeletonJoint.RightHand);

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
            Point3D point = depthGenerator.ConvertRealWorldToProjective(joint.Position);
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
            image.WritePixels(new Int32Rect(Convert.ToInt32(joint.Position.X- 1),
                                            Convert.ToInt32(joint.Position.Y - 1),
                                            3, 3), point, 4, 0);
            image.Unlock();
        }

        public void DrawOrientation(ref WriteableBitmap image, int id, UserGenerator userGenerator, SkeletonJoint joint, Point3D corner)
        {
            SkeletonJointOrientation orientation = new SkeletonJointOrientation();
            SkeletonJointPosition position = new SkeletonJointPosition();

            position = userGenerator.SkeletonCapability.GetSkeletonJointPosition(id, joint);
            orientation = userGenerator.SkeletonCapability.GetSkeletonJointOrientation(id, joint);

            if (position.Confidence != 1 && orientation.Confidence != 1)
            {
                return;
            }

            SkeletonJointPosition v1 = new SkeletonJointPosition();
            SkeletonJointPosition v2 = new SkeletonJointPosition();
            v1.Confidence = v2.Confidence = 1;

            v1.Position = position.Position;
            v2.Position = new Point3D(v1.Position.X + 100 * orientation.X1,
                                      v1.Position.Y + 100 * orientation.Y1,
                                      v1.Position.Z + 100 * orientation.Z1);

            DrawTheLine(ref image, ref v1, ref v2);
            
            v1.Position = position.Position;
            v2.Position = new Point3D(v1.Position.X + 100 * orientation.X2,
                                      v1.Position.Y + 100 * orientation.Y2,
                                      v1.Position.Z + 100 * orientation.Z2);

            DrawTheLine(ref image, ref v1, ref v2);
            
            v1.Position = position.Position;
            v2.Position = new Point3D(v1.Position.X + 100 * orientation.X3,
                                      v1.Position.Y + 100 * orientation.Y3,
                                      v1.Position.Z + 100 * orientation.Z3);
            
            DrawTheLine(ref image, ref v1, ref v2);
            
        }
    }
}
