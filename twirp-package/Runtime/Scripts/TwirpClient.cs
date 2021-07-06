using Google.Protobuf;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Twirp
{
    public class TwirpError
    {
        [JsonProperty("code")]
        public string Code;
        [JsonProperty("msg")]
        public string Message;
        [JsonProperty("meta")]
        public Dictionary<string, string> Meta = new Dictionary<string, string>();
    }

    public class TwirpRequestInstruction<T> : CustomYieldInstruction
    {
        public bool IsDone { get; internal set; }
        public bool IsError { get; internal set; }
        public T Resp { get; internal set; }
        public TwirpError Error { get; internal set; }
        public override bool keepWaiting => !IsDone;
    }

    public class TwirpClient
    {
        private MonoBehaviour mono;
        private string address;
        private int timeout;
        protected string serverPathPrefix;

        public TwirpClient(MonoBehaviour mono, string address, int timeout, string serverPathPrefix)
        {
            this.mono = mono;
            this.address = address;
            this.timeout = timeout;
            this.serverPathPrefix = serverPathPrefix;
        }

        internal TwirpRequestInstruction<T> MakeRequest<T>(string url, IMessage msg) where T : IMessage<T>, new()
        {
            var op = new TwirpRequestInstruction<T>();

            var req = UnityWebRequest.Post(address + serverPathPrefix + '/' + url, Encoding.Default.GetString(msg.ToByteArray()));
            req.timeout = timeout;
            req.SetRequestHeader("Content-Type", "application/protobuf");

            mono.StartCoroutine(HandleRequest<T>(req, op));
            return op;
        }

        private IEnumerator HandleRequest<T>(UnityWebRequest req, TwirpRequestInstruction<T> op) where T : IMessage<T>, new()
        {
            yield return req.SendWebRequest();
            op.IsDone = true;

            if (req.result != UnityWebRequest.Result.Success)
            {
                var e = new TwirpError();
                e.Code = "internal";
                e.Message = req.error;

                op.IsError = true;
                op.Error = e;
            }
            else
            {
                if (req.responseCode == 200)
                {
                    var parser = new MessageParser<T>(() => new T());
                    op.Resp = parser.ParseFrom(req.downloadHandler.data);
                }
                else
                {
                    op.IsError = true;
                    op.Error = JsonConvert.DeserializeObject<TwirpError>(req.downloadHandler.text);
                }
            }
        }
    }
}