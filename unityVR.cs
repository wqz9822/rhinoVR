using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
//using UnityEngine.VR;

public class TCPserver : MonoBehaviour
{
    public GameObject title;
    public GameObject status;
    public GameObject LeftEye;
    public GameObject RightEye;

    private Texture2D l_tex;
    private Texture2D r_tex;

    private Thread readThread;
    private TcpListener server;
    private TcpClient client;

    struct eye_buffer
    {
        public int length;
        public Byte[] buffer;
        public Byte[] last;
    }
    private eye_buffer left_buff;
    private eye_buffer right_buff;
    private Byte[] sizeBuffer;
    private Vector3 dir;
    private Vector3 up;

    private bool connected = false;

    void start_read_thread()
    {
        Debug.Log("Starting Read Thread.");
        readThread = new Thread(new ThreadStart(ReceiveData));
        readThread.IsBackground = true;
        readThread.Start();
        Debug.Log("Read Thread Started");
    }

    void close_read_thread()
    {
        if (readThread != null && readThread.IsAlive)
        {
            if (client != null) client.Close();
            if (server != null) server.Stop();
            readThread.Abort();
        }
        connected = false;
        Debug.Log("Thread closed");
    }

    void restart_read_thread()
    {
        close_read_thread();
        start_read_thread();
    }

    void Start()
    {
        //InputTracking.Recenter();
        dir = Camera.main.transform.forward;
        up  = Camera.main.transform.up;
        LeftEye.SetActive(false);
        RightEye.SetActive(false);

        l_tex = new Texture2D(2, 2);
        r_tex = new Texture2D(2, 2);

        left_buff = new eye_buffer();
        left_buff.last = new Byte[1];
        right_buff = new eye_buffer();
        right_buff.last = new Byte[1];
        sizeBuffer = new Byte[4];

        start_read_thread();

    }

    void Update()
    {
        dir = Camera.main.transform.forward;
        up  = Camera.main.transform.up;
        
        if (connected) {
            title.SetActive(false);
            status.SetActive(false);
            LeftEye.SetActive(true);
            RightEye.SetActive(true);
        } else {
            title.SetActive(true);
            status.SetActive(true);
            LeftEye.SetActive(false);
            RightEye.SetActive(false);
        }
        
        //Debug.DrawLine(Vector3.zero, dir * 4, Color.blue);
        //Debug.DrawLine(Vector3.zero, up * 4, Color.red);
        if (Input.GetMouseButtonDown(0)) restart_read_thread();

        if (left_buff.buffer == null || right_buff.buffer == null)  return; 


        // Only apply the texture when the last byte is the same as received       
        if (left_buff.buffer[left_buff.buffer.Length - 1] == left_buff.last[0])
        {
            l_tex.LoadImage(left_buff.buffer);
            LeftEye.GetComponent<Renderer>().material.mainTexture = l_tex;
        }

        if (right_buff.buffer[right_buff.buffer.Length - 1] == right_buff.last[0])
        {
            r_tex.LoadImage(right_buff.buffer);
            RightEye.GetComponent<Renderer>().material.mainTexture = r_tex;
        }
    }

    void read_buffer( NetworkStream stream )
    {
        // Read left size	
        int nread = stream.Read(sizeBuffer, 0, 4);
        if (nread == 0) return;
        int imgByteSize = BitConverter.ToInt32(sizeBuffer, 0);
        left_buff.length = imgByteSize;
        // Read last byte for checking valid png
        stream.Read(left_buff.last, 0, 1);
        // Read left img
        left_buff.buffer = new Byte[imgByteSize];
        nread = 0;
        while (nread != left_buff.length)
        {
            nread += stream.Read(left_buff.buffer, 0, imgByteSize - nread);
        }

        // Read right size 
        nread = stream.Read(sizeBuffer, 0, 4);
        imgByteSize = BitConverter.ToInt32(sizeBuffer, 0);
        right_buff.length = imgByteSize;
        // Read last byte for checking valid png
        stream.Read(right_buff.last, 0, 1);
        // Read right img
        right_buff.buffer = new Byte[imgByteSize];
        nread = 0;
        while (nread != imgByteSize)
        {
            nread += stream.Read(right_buff.buffer, 0, imgByteSize - nread);
        }
        //Debug.Log("Read " + nread + " bytes for right eye.");
    }

    void ReceiveData()
    {
        try
        {
            Int32 port = 15122;
            IPAddress localAddr = IPAddress.Parse("127.0.0.1");
            server = new TcpListener(localAddr, port);
            server.Start();
            Debug.Log("Waiting for a connection... ");
            client = server.AcceptTcpClient();
            Debug.Log("Connected!");
            connected = true;
            NetworkStream stream = client.GetStream();
            while (true)
            {
                read_buffer(stream);
                Vector3 temp_dir = dir;
                Vector3 temp_up = up;
                for (int i = 0; i < 3; i++)
                {
                    Byte[] temp = BitConverter.GetBytes(temp_dir[i]);
                    stream.Write(temp, 0, 4);
                }
                for (int i = 0; i < 3; i++)
                {
                    Byte[] temp = BitConverter.GetBytes(temp_up[i]);
                    stream.Write(temp, 0, 4);
                }
            }
        }
        catch (SocketException e)
        {
            Debug.Log("SocketException:" + e);
            close_read_thread();
        }
    }

    void OnDestroy()
    {
        close_read_thread();
        Debug.Log("All stoped");
    }
}
