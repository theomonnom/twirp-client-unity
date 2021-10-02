using Google.Protobuf;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Twirp
{
    public class TwirpErrorCode
    {
        public static readonly TwirpErrorCode CANCELED = new TwirpErrorCode("canceled", 408);
        public static readonly TwirpErrorCode UNKNOWN = new TwirpErrorCode("unknown", 500);
        public static readonly TwirpErrorCode INVALID_ARGUMENT = new TwirpErrorCode("invalid_argument", 400);
        public static readonly TwirpErrorCode MALFORMED = new TwirpErrorCode("malformed", 400);
        public static readonly TwirpErrorCode DEADLINE_EXCEEDED = new TwirpErrorCode("deadline_exceeded", 408);
        public static readonly TwirpErrorCode NOT_FOUND = new TwirpErrorCode("not_found", 404);
        public static readonly TwirpErrorCode BAD_ROUTE = new TwirpErrorCode("bad_route", 404);
        public static readonly TwirpErrorCode ALREADY_EXISTS = new TwirpErrorCode("already_exists", 409);
        public static readonly TwirpErrorCode PERMISSION_DENIED = new TwirpErrorCode("permission_denied", 403);
        public static readonly TwirpErrorCode UNAUTHENTICATED = new TwirpErrorCode("unauthenticated", 401);
        public static readonly TwirpErrorCode RESOURCE_EXHAUSTED = new TwirpErrorCode("resource_exhausted", 429);
        public static readonly TwirpErrorCode FAILED_PRECONDITION = new TwirpErrorCode("failed_precondition", 412);
        public static readonly TwirpErrorCode ABORTED = new TwirpErrorCode("aborted", 409);
        public static readonly TwirpErrorCode OUT_OF_RANGE = new TwirpErrorCode("out_of_range", 400);
        public static readonly TwirpErrorCode UNIMPLEMENTED = new TwirpErrorCode("unimplemented", 501);
        public static readonly TwirpErrorCode INTERNAL = new TwirpErrorCode("internal", 500);
        public static readonly TwirpErrorCode UNAVAILABLE = new TwirpErrorCode("unavailable", 503);
        public static readonly TwirpErrorCode DATALOSS = new TwirpErrorCode("dataloss", 500);

        public readonly string Code;
        public readonly int HttpStatus;

        public TwirpErrorCode(string code, int httpStatus)
        {
            Code = code;
            HttpStatus = httpStatus;
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
        public bool IsDone;
        public bool IsError;
        public T Resp;
        public TwirpError Error;
        public override bool keepWaiting => !IsDone;
    }

    public abstract class TwirpHook
    {
        public abstract IEnumerator RequestStarted<I>(TwirpClient client, string path, UnityWebRequest req, I msg) where I : IMessage<I>;
        public abstract IEnumerator RequestFinished<I, O>(TwirpClient client, string path, UnityWebRequest req, I msg, TwirpRequestInstruction<O> op) where I : IMessage<I> where O : IMessage<O>, new();
    }

    public class TwirpClient
    {
        private MonoBehaviour mono;
        private string address;
        private int timeout;
        protected string serverPathPrefix;
        private TwirpHook hook;

        public TwirpClient(MonoBehaviour mono, string address, int timeout, string serverPathPrefix, TwirpHook hook)
        {
            this.mono = mono;
            this.address = address;
            this.timeout = timeout;
            this.serverPathPrefix = serverPathPrefix;
            this.hook = hook;
        }

        public TwirpRequestInstruction<O> MakeRequest<I, O>(string path, I msg) where I : IMessage<I> where O : IMessage<O>, new()
        {
            var op = new TwirpRequestInstruction<O>();
            mono.StartCoroutine(HandleRequest(path, msg, op));
            return op;
        }

        private IEnumerator HandleRequest<I, O>(string path, I msg, TwirpRequestInstruction<O> op) where I : IMessage<I> where O : IMessage<O>, new()
        {
            using var req = new UnityWebRequest(address + serverPathPrefix + '/' + path, UnityWebRequest.kHttpVerbPOST);
            req.timeout = timeout;

            var upload = new UploadHandlerRaw(msg.ToByteArray());
            upload.contentType = "application/protobuf";
            req.uploadHandler = upload;

            var download = new DownloadHandlerBuffer();
            req.downloadHandler = download;

            yield return hook?.RequestStarted(this, path, req, msg);
            yield return req.SendWebRequest();
            op.IsDone = true;

            if (req.responseCode == 200)
            {
                var parser = new MessageParser<O>(() => new O());
                op.Resp = parser.ParseFrom(req.downloadHandler.data);
            }
            else
            {
                op.IsError = true;
                try
                {
                    op.Error = JsonUtility.FromJson<TwirpError>(req.downloadHandler.text);
                }
                catch
                {
                    var e = new TwirpError();
                    e.code = TwirpErrorCode.INTERNAL.Code;
                    e.message = req.error + ": " + req.downloadHandler.text;
                    op.Error = e;
                }
            }

            yield return hook?.RequestFinished(this, path, req, msg, op);
        }
    }
}