using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.QrCode;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine.Networking;
using System.IO;
using System.Runtime.InteropServices;

public class QRScanner : MonoBehaviour
{
    //Camera Setting
    private bool camAvailable;
    private WebCamTexture backCam;
    private WebCamTexture frontCam;
    private Texture defaultBackground;
    private Rect screenRect;

    //Image Setting
    public Texture2D texture;
    public RawImage background;
    public AspectRatioFitter fit;
    public Material colorTransformMaterial;
    public Text colorTextMesh;
    private TexturePool imgPool;
    private Texture2D img;

    //
    private string type = "";
    private string level = "";
    private string colorrr = "";
    private string colorName = "detecting...";
    private bool expFin = false; //看實驗有沒有完成
    private string expFText = "Experiment Finished!";
    private double factor = 1;
    private Color32 lastProcessedColor;

    //IP & Port
    public string imageServerIP = "192.168.68.111"; //HoloLens IP
    public int imageServerPort = 5010; //Self-define
    public string bboxClientIP = "192.168.68.102"; //Remote PC IP
    public int bboxClientPort = 12345;

    //Server & Client
    private Socket imgServer;
    private Socket imgClient;
    private Socket bbxClient;

    //Boolean
    private bool isImgCoroutine = false;
    private bool isBbxCoroutine = false;
    private bool imgConnected = false;
    private bool bbxConnected = false;

    //Header & Data 
    //TODO: 為什麼header傳不出去
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct IMAGE_HEADER
    {
        public byte Cookie;
        public byte VersionMajor;
        public byte VersionMinor;
        public uint FrameType;
        public long Timestamp;
        public uint ImageWidth;
        public uint ImageHeight;
        public uint PixelStride;
        public uint RowStride;


        // Padding to ensure the struct size is 32 bytes
        public byte Padding;
    }
    private byte[] imgHeaderBytes = new byte[32];
    private byte[] imgBytes;
    private string bbxHeaderFormat = "@36f";
    private int bbxHeaderSize = 36 * sizeof(float);

    //Bbox Setting
    private int numberOfBoxes = 2; //Define max Boxes
    private float[] bbxBuffer = new float[36];
    public struct BBox
    {
        public string cls;
        public float x;
        public float y;
        public float w;
        public float h;
    }
    public List<BBox> bboxes = new List<BBox>();

    // Use this for initialization
    private void Start()
    {
        SetImageServer();
        StartCoroutine(ImageCoroutine());


        colorTextMesh = GetComponent<Text>();
        backCam = new WebCamTexture();

        //For no camera PC:)
        string camName = WebCamTexture.devices[1].name;
        backCam = new WebCamTexture(camName, Screen.width, Screen.height, 30);

        backCam.requestedHeight = Screen.height;
        backCam.requestedWidth = Screen.width;
        if (backCam != null)
        {
            backCam.Play();
            background.texture = backCam;
            camAvailable = true;

            StartCoroutine(colorEveryFiveSeconds());
        }
        int width = backCam.width;
        int height = backCam.height;
        texture = new Texture2D(width, height);
        imgPool = new TexturePool();
        img = new Texture2D(width, height);

        //Init Header
        IMAGE_HEADER header = new IMAGE_HEADER
        {
            Cookie = 123,
            VersionMajor = 1,
            VersionMinor = 2,
            FrameType = (uint)3,
            Timestamp = (long)DateTime.Now.Ticks,
            ImageWidth = (uint)backCam.width,
            ImageHeight = (uint)backCam.height,
            PixelStride = 3,
            RowStride = (uint)backCam.width * 3
        };
        imgHeaderBytes = StructureToByteArray(header);
        Debug.Log("INFO:  IMG w/ " + imgHeaderBytes.Length);
    }

    //Image Sending
    public void SetImageServer()
    {
        imgServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Debug.Log($"INFO: Setup IMAGE Server at {imageServerIP} on {imageServerPort}");
        imgServer.Bind(new IPEndPoint(IPAddress.Parse(imageServerIP), imageServerPort));
        Debug.Log("INFO: Setup IMAGE Server");

        imgServer.Listen(5);
        Debug.Log("INFO: Waiting for Connection...");
    }

    private IEnumerator ImageCoroutine()
    {
        Debug.Log($"INFO: Image Coroutine...");
        isImgCoroutine = true;


        while (isImgCoroutine)
        {
            Debug.Log($"INFO: IMGing...");


            yield return StartCoroutine(ImgConnectCoroutine());

            if (imgConnected && imgClient != null)
            {
                while (imgConnected)
                {
                    img = imgPool.Get(backCam);
                    img.SetPixels(backCam.GetPixels());
                    img.Apply();

                    var send = StartCoroutine(ImgSendCoroutine());
                    var receive = StartCoroutine(BbxCoroutine());
                    yield return send;
                    yield return receive;

                    yield return null;
                }
            }
            else
            {
                yield return null;
            }
        }
    }

    private IEnumerator ImgConnectCoroutine()
    {
        Debug.Log("INFO: Waiting for Connection...");


        float timeout = 10f; // Set a timeout of 10 seconds
        float elapsedTime = 0f;


        while (!imgServer.Poll(0, SelectMode.SelectRead))
        {
            elapsedTime += Time.deltaTime;


            if (elapsedTime >= timeout)
            {
                Debug.Log("INFO: Connection timeout");
                yield break; // Break out of the coroutine if the timeout is reached
            }
            //Debug.Log("INFO: No Connection");

            yield return null;
        }


        imgClient = imgServer.Accept();
        EndPoint imgClientEP = imgClient.RemoteEndPoint;
        string imgClientIP = imgClientEP.AddressFamily.ToString();
        Debug.Log($"INFO: Connection from {imgClient} at {imgClientIP}");
        imgConnected = true;
        yield break;
    }

    private IEnumerator ImgSendCoroutine()
    {
        Debug.Log("INFO: Connected. Ready to send!");

        imgBytes = img.EncodeToPNG();

        //Send Header
        //Debug.Log("INFO: Sent IMG w/ " + imgHeaderBytes.Length);
        //imgClient.Send(imgHeaderBytes);

        imgClient.Send(BitConverter.GetBytes(imgBytes.Length)); //Send img size to client (充當header)
        imgClient.Send(imgBytes); //Send img data
        Debug.Log("------------- INFO: Sent IMG! -------------");

        yield return null;
    }

    //Bbox Receiving
    private IEnumerator BbxCoroutine()
    {
        Debug.Log($"INFO: BBX Coroutine");
        isBbxCoroutine = true;

        bbxClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Debug.Log($"Connecting to BBX Server at {bboxClientIP} {bboxClientPort}...");
        bbxClient.Connect(new IPEndPoint(IPAddress.Parse(bboxClientIP), bboxClientPort));
        while (!bbxClient.Connected) // 非阻塞检查是否连接成功
            yield break;
        Debug.Log("INFO: Connected to BBX Server!");

        ReceiveBbx();
        yield return null;
    }

    private IEnumerator BbxConnectCoroutine()
    {
        bbxClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Debug.Log($"Connecting to BBX Server at {bboxClientIP} {bboxClientPort}...");
        bbxClient.Connect(new IPEndPoint(IPAddress.Parse(bboxClientIP), bboxClientPort));
        while (!bbxClient.Connected) // 非阻塞检查是否连接成功
            yield break;
        Debug.Log("INFO: Connected to BBX Server!");
        bbxConnected = true;
    }

    void ReceiveBbx()
    {
        Debug.Log($"INFO: Receive data from {bbxClient}");


        if (bbxClient != null)
        {
            Debug.Log("Receiving Data");

            byte[] headerBytes = new byte[bbxHeaderSize];
            Debug.Log(headerBytes.ToString());

            bbxClient.Receive(headerBytes);
            Debug.Log(headerBytes.ToString());



            //while (bytesRead < bbxHeaderSize)
            //{
            //    bytesRead += bbxStream.Read(headerBytes, bytesRead, bbxHeaderSize - bytesRead);
            //}


            // 將 header 的 byte 數組轉換為浮點數陣列
            for (int i = 0; i < 36; i++)
            {
                bbxBuffer[i] = BitConverter.ToSingle(headerBytes, i * sizeof(float));
                //Debug.Log($"{bbxBuffer[i]}");
            }
        }
    }

    //Get Color
    private IEnumerator colorEveryFiveSeconds()
    {
        while (true) // 無限循環
        {
            yield return new WaitForSeconds(5f); // 等待五秒


            if (camAvailable)
            {
                int centerX = Screen.width / 2;
                int centerY = Screen.height / 2;


                Color32 centerColor = backCam.GetPixel(centerX, centerY);


                if (centerColor.Equals(lastProcessedColor))
                {
                    continue;
                }


                lastProcessedColor = centerColor;


                colorrr = "中間顏色 R: " + centerColor.r + ", G: " + centerColor.g + ", B: " + centerColor.b;
                Debug.Log("中間顏色 R: " + centerColor.r + ", G: " + centerColor.g + ", B: " + centerColor.b);
                if (centerColor.r + 10 < centerColor.g && centerColor.g > centerColor.b + 10)
                {
                    expFin = true;
                }
                else
                {
                    expFin = false;

                }

                string rgbString = $"rgb({centerColor.r},{centerColor.g},{centerColor.b})";
                StartCoroutine(GetColorInfoFromAPI(rgbString));






            }
        }
    }

    private IEnumerator GetColorInfoFromAPI(string rgbString)
    {
        string apiUrl = $"https://www.thecolorapi.com/id?rgb={rgbString}";


        UnityWebRequest request = UnityWebRequest.Get(apiUrl);


        yield return request.SendWebRequest();


        if (request.result != UnityWebRequest.Result.ConnectionError && request.result != UnityWebRequest.Result.ProtocolError)
        {
            string responseText = request.downloadHandler.text;
            // Parse JSON response using Unity's JSON utility
            ColorApiResponse colorInfo = JsonUtility.FromJson<ColorApiResponse>(responseText);
            colorName = colorInfo.name.value;
            Debug.Log($"Color Name: {colorName}");
        }
        else
        {
            Debug.LogWarning($"API request failed: {request.error}");
        }
    }


    private Texture2D MakeTex(int width, int height, Color outlineColor, Color col)
    {
        Color[] pix = new Color[width * height];


        // Fill with transparent color
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = Color.clear;
        }


        // Set the outline pixels
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || y == 0 || x == width - 1 || y == height - 1 || x == 1 || y == 1 || x == width - 2 || y == height - 2 || x == 2 || y == 2 || x == width - 3 || y == height - 3)
                {
                    pix[y * width + x] = outlineColor;  // set outline pixels to the outline color
                }
            }
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    void DrawBoxes()
    {
        for (int i = 0; i < numberOfBoxes; i++)
        {
            int temp_cls = (int)bbxBuffer[6 * i + 0];
            float temp_x = bbxBuffer[6 * i + 1];
            float temp_y = bbxBuffer[6 * i + 2];
            float temp_h = (bbxBuffer[6 * i + 3] / 2);
            float temp_w = (bbxBuffer[6 * i + 4] / 2);
            float temp_conf = bbxBuffer[6 * i + 5];

            if (temp_cls != -1 && temp_w != 0)
            {
                Debug.Log($"cls:{temp_cls}, x:{temp_x}, y:{temp_y}, w:{temp_w}, h:{temp_h}");
            }

            colorTransformMaterial.SetVector($"_Rect{i+1}", new Vector4(temp_x - temp_w, temp_y - temp_h, temp_x + temp_w, temp_y + temp_h));
        }
    }

    // Update is called once per frame
    private void OnGUI()
    {
        if (!camAvailable)
            return;


        Color32[] pixelData = backCam.GetPixels32();


        int width = backCam.width;
        int height = backCam.height;


        texture.SetPixels32(pixelData);

        //Scanning QRcode
        try
        {
            
            IBarcodeReader barcodeReader = new BarcodeReader();

            // decode the current frame
            var result = barcodeReader.Decode(backCam.GetPixels32(),
                backCam.width, backCam.height);
            if (result != null)
            {
                //UnityEngine.Debug.Log(result);
                UnityEngine.Debug.Log("掃到哭阿扣啦");
                if (result.Text[0].ToString() == "B")
                {
                    type = "protanomalous";
                }
                else if (result.Text[0].ToString() == "C")
                {
                    type = "deuteranomalous";
                }
                else if (result.Text[0].ToString() == "D")
                {
                    type = "tritanomalous";
                }
                else if (result.Text[0].ToString() == "A")
                {
                    type = "normal";
                    level = "normal";
                }


                if (type != "normal")
                {
                    if (result.Text[1].ToString() == "1")
                    {
                        level = "severe";
                    }
                    else if (result.Text[1].ToString() == "2")
                    {
                        level = "moderate";
                    }
                    else if (result.Text[1].ToString() == "3")
                    {
                        level = "mild";
                    }
                }

                UnityEngine.Debug.Log("色盲型態 : " + type + " " + level);
            }
        }
        catch (Exception ex) { Debug.LogWarning(ex.Message); }

        //Define type & set factor
        if (level == "severe")
        {
            // severe
            factor = 1.5;
        }
        else if (level == "moderate")
        {
            // moderate
            factor = 1.3;
        }
        else if (level == "mild")
        {
            // mild;
            factor = 1.15;
        }


        float ratio = (float)backCam.width / (float)backCam.height;
        fit.aspectRatio = ratio;


        float scaleY = backCam.videoVerticallyMirrored ? -1f : 1f;
        background.rectTransform.localScale = new Vector3(1f, scaleY, 1f);    //非鏡像
                                                                              //background.rectTransform.localScale = new Vector3(-1f, scaleY, 1f);    //鏡像

        int orient = -backCam.videoRotationAngle;
        background.rectTransform.localEulerAngles = new Vector3(0, 0, orient);


        int w = Screen.width, h = Screen.height;
        GUIStyle style = new GUIStyle();


        GUIStyle color_style = new GUIStyle();
        color_style.alignment = TextAnchor.MiddleCenter;
        color_style.fontSize = 26;
        color_style.fontStyle = FontStyle.Bold;
        //color_style.normal.textColor = new Color(25, 25, 112);
        color_style.normal.background = MakeTex(600, 1, new Color(255 / 255f, 255 / 255f, 255 / 255f), new Color(255 / 255f, 255 / 255f, 255 / 255f));
        //color_style.normal.background = EditorGUIUtility.whiteTexture;


        color_style.normal.textColor = new Color(25 / 255f, 25 / 255f, 112 / 255f);


        //UnityEngine.Debug.Log("width : " + Screen.width + " " + (Screen.width / 2 - 300 / 2 + 100));


        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = 24;
        style.normal.textColor = new Color(0, 0, 205);


        string text = type + " " + level + "\n";
        Rect textRect = new Rect(Screen.width / 2 - 300 / 2 + 100, Screen.height / 2 - 300 / 2, Screen.width, Screen.height);
        Rect color_textRect = new Rect(Screen.width / 2 - 400 / 2 + 100, Screen.height / 2 - 300 / 2, 200, 30);
        Rect exfinishRect = new Rect(Screen.width / 2 - 400 / 2 + 100, Screen.height / 2 - 250 / 2, 200, 20);

        if (expFin == true)
        {
            GUI.Label(exfinishRect, expFText, style);
        }

        GUI.Label(color_textRect, colorName, color_style);
        GUILayout.Label(text, style);


        if (bbxBuffer != new float[36])
        {
            DrawBoxes();
        }

        return;
    }

    void Update()
    {
        if (factor != 1)
        {
            float redMultiplier = 1.0f; // 這裡設定你想要的值
            float greenMultiplier = 1.0f; // 這裡設定你想要的值
            float blueMultiplier = 1.0f; // 這裡設定你想要的值
            if (colorTransformMaterial == null)
            {
                colorTransformMaterial = new Material(Shader.Find("Custom/NewSurfaceShader"));
            }

            if (type == "protanomalous")
            {
                redMultiplier = (float)factor;
                blueMultiplier = (float)factor;
            }
            else if (type == "deuteranomalous")
            {
                greenMultiplier = (float)factor;
                blueMultiplier = (float)factor;
            }
            else if (type == "tritanomalous")
            {
                redMultiplier = (float)factor;
                greenMultiplier = (float)factor;
            }

            colorTransformMaterial.SetFloat("_RedMultiplier", redMultiplier);
            colorTransformMaterial.SetFloat("_GreenMultiplier", greenMultiplier);
            colorTransformMaterial.SetFloat("_BlueMultiplier", blueMultiplier);
            factor = 1;
        }
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination, colorTransformMaterial);
    }


    //打包结构体
    static byte[] PackStruct<T>(T structure) where T : struct
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
        byte[] data = new byte[size];


        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            byte[] rawData = StructureToByteArray(structure);
            writer.Write(rawData);
        }


        return data;
    }


    // 解包结构体
    static T UnpackStruct<T>(byte[] data) where T : struct
    {
        T structure = new T();


        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            byte[] rawData = reader.ReadBytes(data.Length);
            structure = ByteArrayToStructure<T>(rawData);
        }


        return structure;
    }


    // 将结构体转换为字节数组
    static byte[] StructureToByteArray(object structure)
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf(structure);
        Debug.Log(size);
        byte[] data = new byte[size];
        IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);


        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(structure, ptr, true);
            System.Runtime.InteropServices.Marshal.Copy(ptr, data, 0, size);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
        }


        return data;
    }


    // 将字节数组转换为结构体
    static T ByteArrayToStructure<T>(byte[] data) where T : struct
    {
        T structure = new T();
        int size = System.Runtime.InteropServices.Marshal.SizeOf(structure);


        IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);


        try
        {
            System.Runtime.InteropServices.Marshal.Copy(data, 0, ptr, size);
            structure = (T)System.Runtime.InteropServices.Marshal.PtrToStructure(ptr, typeof(T));
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
        }


        return structure;
    }


}


[Serializable]
public class ColorApiResponse
{
    public ColorName name;
}


[Serializable]
public class ColorName
{
    public string value;
}

public class TexturePool
{
    private Stack<Texture2D> inactive = new Stack<Texture2D>();
    public Texture2D Get(WebCamTexture Cam)
    {
        //Debug.Log($"INFO: Getting img");


        if (inactive.Count > 0)
        {
            Debug.Log($"INFO: no img");


            return inactive.Pop();
        }
        //Debug.Log($"INFO: Got img!");
        return new Texture2D(Cam.width, Cam.height);
    }


    public void Release(Texture2D tex)
    {
        inactive.Push(tex);
    }


}
