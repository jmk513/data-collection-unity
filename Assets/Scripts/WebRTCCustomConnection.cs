using UnityEngine;
using Unity.WebRTC;
using NativeWebSocket;
using System.Text;
using System.Collections;
using PassthroughCameraSamples;

public class WebRTCClient : MonoBehaviour
{
    private RTCPeerConnection pc;

    private VideoStreamTrack videoTrack;

    private WebCamTexture _webcamTexture;
    private WebSocket ws;

    void Start()
    {
        StartCoroutine(ConnectToSignalingServer());
    }

    IEnumerator ConnectToSignalingServer()
    {
        ws = new WebSocket("ws://147.46.219.55:7779/ws"); // ← 서버 IP 수정

        ws.OnOpen += () =>
        {
            Debug.Log("WebSocket connected");

            var config = GetSelectedSdpSemantics();
            pc = new RTCPeerConnection(ref config);

            // ✅ 웹캠 시작
            _webcamTexture = webCamTextureManager.WebCamTexture;
            if (_webcamTexture != null)
            {
                print($"WebCamTexture dimensions: {_webcamTexture.width}x{_webcamTexture.height}");
            }
            else
            {
                Debug.LogError("WebCamTexture is null at Start.");
            }

            // ✅ WebRTC 트랙으로 변환해서 추가
            videoTrack = new VideoStreamTrack(_webcamTexture);
            pc.AddTrack(videoTrack);

            StartCoroutine(SendOffer());
        };

        ws.OnMessage += (bytes) =>
        {
            var msg = Encoding.UTF8.GetString(bytes);
            Debug.Log($"Received from server: {msg}");

            var sdpDict = JsonUtility.FromJson<SDPMessage>(msg);
            var desc = new RTCSessionDescription
            {
                type = RTCSdpType.Answer,
                sdp = sdpDict.sdp.Replace("\\n", "\n")
            };

            pc.SetRemoteDescription(ref desc);
        };

        yield return ws.Connect();
    }

    IEnumerator SendOffer()
    {
        var op = pc.CreateOffer();
        yield return op;

        var desc = op.Desc;
        var setOp = pc.SetLocalDescription(ref desc);
        yield return setOp;

        var sdpMsg = new SDPMessage
        {
            type = "offer",
            sdp = desc.sdp
        };

        string json = JsonUtility.ToJson(sdpMsg);
        ws.SendText(json);
    }

    private static RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        config.iceServers = new[] {
            new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
        };
        return config;
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    private void OnApplicationQuit()
    {
        videoTrack?.Dispose();
        ws?.Close();
        pc?.Close();
    }

    [System.Serializable]
    private class SDPMessage
    {
        public string type;
        public string sdp;
    }
    
    [SerializeField] private WebCamTextureManager webCamTextureManager;
}

