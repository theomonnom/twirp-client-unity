using Google.Protobuf;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Twirp
{
    public class ErrorCode
    {
        public static readonly ErrorCode CANCELED = new ErrorCode("canceled", 408);
        public static readonly ErrorCode UNKNOWN = new ErrorCode("unknown", 500);
        public static readonly ErrorCode INVALID_ARGUMENT = new ErrorCode("invalid_argument", 400);
        public static readonly ErrorCode MALFORMED = new ErrorCode("malformed", 400);
        public static readonly ErrorCode DEADLINE_EXCEEDED = new ErrorCode("deadline_exceeded", 408);
        public static readonly ErrorCode NOT_FOUND = new ErrorCode("not_found", 404);
        public static readonly ErrorCode BAD_ROUTE = new ErrorCode("bad_route", 404);
        public static readonly ErrorCode ALREADY_EXISTS = new ErrorCode("already_exists", 409);
        public static readonly ErrorCode PERMISSION_DENIED = new ErrorCode("permission_denied", 403);
        public static readonly ErrorCode UNAUTHENTICATED = new ErrorCode("unauthenticated", 401);
        public static readonly ErrorCode RESOURCE_EXHAUSTED = new ErrorCode("resource_exhausted", 429);
        public static readonly ErrorCode FAILED_PRECONDITION = new ErrorCode("failed_precondition", 412);
        public static readonly ErrorCode ABORTED = new ErrorCode("aborted", 409);
        public static readonly ErrorCode OUT_OF_RANGE = new ErrorCode("out_of_range", 400);
        public static readonly ErrorCode UNIMPLEMENTED = new ErrorCode("unimplemented", 501);
        public static readonly ErrorCode INTERNAL = new ErrorCode("internal", 500);
        public static readonly ErrorCode UNAVAILABLE = new ErrorCode("unavailable", 503);
        public static readonly ErrorCode DATALOSS = new ErrorCode("dataloss", 500);

        public readonly string Code;
        public readonly int HttpStatus;

        public ErrorCode(string code, int httpStatus)
        {
            Code = code;
            HttpStatus = httpStatus;
        }

        public override int GetHashCode()
        {
            return Code.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (GetType() == obj.GetType())
                return Code == (obj as ErrorCode).Code;

            if (obj is string)
                return Code == (obj as string);

            return base.Equals(obj);
        }
    }

    [Serializable]
    public class TwirpError
    {
        public string code;
        public string message;
        public Dictionary<string, string> meta = new Dictionary<string, string>();
    }

    public class TwirpRequestInstruction<T> : CustomYieldInstruction
    {
        public bool IsDone { get; internal set; }
        public bool IsError { get; internal set; }
        public T Resp { get; internal set; }
        public TwirpError Error { get; internal set; }
        public override bool keepWaiting => !IsDone;
    }

    public delegate void TwirpMiddleware(UnityWebRequest req, IMessage msg);

    public class TwirpClient
    {
        private MonoBehaviour mono;
        private string address;
        private int timeout;
        private TwirpMiddleware[] middlewares;
        protected string serverPathPrefix;

        public TwirpClient(MonoBehaviour mono, string address, int timeout, string serverPathPrefix, params TwirpMiddleware[] middlewares)
        {
            this.mono = mono;
            this.address = address;
            this.timeout = timeout;
            this.serverPathPrefix = serverPathPrefix;
            this.middlewares = middlewares;
        }

        protected TwirpRequestInstruction<T> MakeRequest<T>(string url, IMessage msg) where T : IMessage<T>, new()
        {
            var op = new TwirpRequestInstruction<T>();
            var req = new UnityWebRequest(address + serverPathPrefix + '/' + url, UnityWebRequest.kHttpVerbPOST);
            req.timeout = timeout;

            var upload = new UploadHandlerRaw(msg.ToByteArray());
            upload.contentType = "application/protobuf";
            req.uploadHandler = upload;

            var download = new DownloadHandlerBuffer();
            req.downloadHandler = download;

            foreach(var m in middlewares)
            {
                m(req, msg);
            }

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
                e.code = ErrorCode.INTERNAL.Code;
                e.message = req.error;

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
                    op.Error = JsonUtility.FromJson<TwirpError>(req.downloadHandler.text);
                }
            }
        }
    }
}