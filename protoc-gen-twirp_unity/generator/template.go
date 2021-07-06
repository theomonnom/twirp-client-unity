package generator

import "text/template"

type TwirpTemplateVariables struct {
	FileName  string
	Namespace string
	Services  []*TwirpService
}

type TwirpService struct {
	ServiceURL string
	Name       string
	Comment    string
	Methods    []*TwirpMethod
}

type TwirpMethod struct {
	ServiceURL  string
	ServiceName string
	Name        string
	Comment     string
	Input       string
	Output      string
}

type TwirpImport struct {
	From   string
	Import string
}

var TwirpTemplate = template.Must(template.New("TwirpTemplate").Parse(`
using {{.Namespace}};
using UnityEngine;

// source: {{.FileName}}
namespace Twirp {
	{{range .Services}}
	public class {{.Name}}Client : TwirpClient {

		public {{.Name}}Client(MonoBehaviour mono, string url, int timeout, string serverPathPrefix="twirp") : base(mono, url, timeout, serverPathPrefix) {

		}
		{{range .Methods}}
		public TwirpRequestInstruction<{{.Output}}> {{.Name}}({{.Input}} request){
			return this.MakeRequest<{{.Output}}>("{{.ServiceURL}}/{{.Name}}", request);
		}{{end}}
	}{{end}}
}
`))
