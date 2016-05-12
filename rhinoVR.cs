using System;
using Rhino;
using Rhino.Display;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.DocObjects;
using System.IO;

using System.Drawing;
using System.Drawing.Imaging;

using System.Net.Sockets;
using System.Threading;
using System.Windows.Threading;

namespace MyProject1
{
    [System.Runtime.InteropServices.Guid("e0218468-5007-498d-bed0-b29d9f837d5f")]
    public class MyProject1Command : Command
    {
        MemoryStream memStreamLeft, memStreamRight;
        Bitmap rhCaptureLeft, rhCaptureRight;

        TcpClient client;
        Dispatcher mainDispatcher;
        Thread sendThread;

        RhinoView rhViewLeft, rhViewRight;
        RhinoViewport rhViewLeftPort, rhViewRightPort;
        ViewportInfo rhViewLeftInfo, rhViewRightInfo;
        Point3d pt0;
        Byte[] bytesLeftEye, bytesLeftRight;
        Vector3f cam_dir, cam_up;
        Point3f cam_left_pos, cam_right_pos;

        public MyProject1Command()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static MyProject1Command Instance
        {
            get; private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "rhinoVR"; }
        }

        private void read_data(NetworkStream stream)
        {
            float[] temp = new float[3];
            Byte[] t = new Byte[4];

            for (int i = 0; i < 3; i++)
            {
                stream.Read(t, 0, 4);
                temp[i] = BitConverter.ToSingle(t, 0);
            }
            cam_dir.X = temp[0];
            cam_dir.Y = temp[2];
            cam_dir.Z = temp[1];

            
            for (int i = 0; i < 3; i++)
            {
                stream.Read(t, 0, 4);
                temp[i] = BitConverter.ToSingle(t, 0);
            }
            
            cam_up.X = temp[0];
            cam_up.Y = temp[2];
            cam_up.Z = temp[1];


        }

        private void send_data(NetworkStream stream, Byte[] bytes_array)
        {
            int len = bytes_array.Length;
            if (len == 0) return;
            Byte[] len_array = BitConverter.GetBytes(len);
            Byte[] check_array = new Byte[1];
            check_array[0] = bytes_array[len - 1];
            stream.Write(len_array, 0, len_array.Length);
            stream.Write(check_array, 0, check_array.Length);
            stream.Write(bytes_array, 0, bytes_array.Length);

        }

        private void send_thread(Object obj)
        {
            RhinoDoc doc = (RhinoDoc)obj;
            try
            { 
                client = new TcpClient("127.0.0.1", 15122);
                NetworkStream stream = client.GetStream();

                while (true)
                {
                    rhViewLeftInfo.Camera35mmLensLength = 15.0;
                    rhViewLeftInfo.SetCameraUp(cam_up);
                    rhViewLeftInfo.SetCameraDirection(cam_dir);
                    rhViewLeftInfo.SetCameraLocation(cam_left_pos);

                    rhViewRightInfo.Camera35mmLensLength = 15.0;
                    rhViewRightInfo.SetCameraUp(cam_up);
                    rhViewRightInfo.SetCameraDirection(cam_dir);
                    rhViewRightInfo.SetCameraLocation(cam_right_pos);


                    mainDispatcher.Invoke((Action)(() =>
                    {               
                        rhViewLeftPort.SetViewProjection(rhViewLeftInfo, true);
                        rhViewRightPort.SetViewProjection(rhViewRightInfo, true);
                        rhCaptureLeft  = DisplayPipeline.DrawToBitmap(rhViewLeftPort, 800, 600);
                        rhCaptureRight = DisplayPipeline.DrawToBitmap(rhViewRightPort, 800, 600);
                        rhViewLeft.Redraw();
                        rhViewRight.Redraw();
                    }));

                    if (rhCaptureLeft == null || rhCaptureRight == null) continue;
                    memStreamLeft = new MemoryStream();
                    memStreamRight = new MemoryStream();
                    rhCaptureLeft.Save(memStreamLeft, ImageFormat.Png);
                    rhCaptureRight.Save(memStreamRight, ImageFormat.Png);
                    bytesLeftEye = memStreamLeft.ToArray();
                    bytesLeftRight = memStreamRight.ToArray();
                    send_data(stream, bytesLeftEye);
                    send_data(stream, bytesLeftRight);
                    read_data(stream);
                    Thread.Sleep(1);
                }
            }
            catch (IOException e)
            {
                client.Close();
                mainDispatcher.Invoke((Action)(() =>
                {
                    RhinoApp.WriteLine("VR Thread Closed.");
                    rhViewLeft.Close();
                    rhViewRight.Close();
                }));
                sendThread.Abort();
            }
            catch (SocketException e)
            {
                client.Close();
                mainDispatcher.Invoke((Action)(() =>
                {
                    RhinoApp.WriteLine("VR Thread Closed.");
                    rhViewLeft.Close();
                    rhViewRight.Close();

                }));
                sendThread.Abort();
            }

        }

        private void setup_view(RhinoDoc doc)
        {
            RhinoView p = doc.Views.Find("Perspective", false);
            pt0 = p.ActiveViewport.CameraLocation;
            string targetLeftName = "LeftEye";
            string targetRightName = "RightEye";
            int targetWidth = 400;
            int targetHeight = 300;
            Rectangle rect_l = new Rectangle(0, 0, targetWidth, targetHeight);
            Rectangle rect_r = new Rectangle(targetWidth, 0, targetWidth, targetHeight);
            doc.Views.Add( targetLeftName, DefinedViewportProjection.Perspective, rect_l, true);
            doc.Views.Add(targetRightName, DefinedViewportProjection.Perspective, rect_r, true);
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            setup_view(doc);
            mainDispatcher = Dispatcher.CurrentDispatcher;
            rhViewLeft = doc.Views.Find("LeftEye", false);
            rhViewRight = doc.Views.Find("RightEye", false);
            rhViewLeftPort = rhViewLeft.ActiveViewport;
            rhViewRightPort = rhViewRight.ActiveViewport;
            rhViewLeftPort.DisplayMode = DisplayModeDescription.FindByName("Shaded");
            rhViewRightPort.DisplayMode = DisplayModeDescription.FindByName("Shaded");
            rhViewLeftInfo = new ViewportInfo(rhViewLeftPort);
            rhViewRightInfo = new ViewportInfo(rhViewRightPort);
            string unit = doc.GetUnitSystemName(true, true, true, true);
            RhinoApp.WriteLine(unit);
            float scale = 1.0f;
            switch (unit)
            {
                case "m":
                    scale = 1000.0f;
                    break;
                case "cm":
                    scale = 10.0f;
                    break;
                case "mm":
                    scale = 1.0f;
                    break;
                default:
                    break;
            }
            float IPD = 64.0f / (scale * 2.0f);
            float height = 1700.0f / scale;
            cam_left_pos  = new Point3f(-IPD + (float)pt0.X, (float)pt0.Y, height);
            cam_right_pos = new Point3f(+IPD + (float)pt0.X, (float)pt0.Y, height);

            RhinoApp.WriteLine("Starting Treads");
            sendThread = new Thread(new ParameterizedThreadStart(send_thread));
            sendThread.IsBackground = true;
            sendThread.Start(doc);
            RhinoApp.WriteLine("Threads Started");
            return Result.Success;
        }
    }
}
