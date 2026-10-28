using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
var baseUrl=args[0];var cookiePath=args[1];var output=args[2];Directory.CreateDirectory(output);
var start=new ProcessStartInfo(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"){UseShellExecute=false,CreateNoWindow=true};
foreach(var arg in new[]{"--headless","--disable-gpu","--no-first-run","--no-default-browser-check","--remote-debugging-port=9224","--user-data-dir="+Path.Combine(output,"edge-"+Guid.NewGuid().ToString("N")),"about:blank"})start.ArgumentList.Add(arg);
using var browser=Process.Start(start)!;
try{
 using var http=new HttpClient();string? address=null;
 for(var i=0;i<40;i++){try{address=JsonNode.Parse(await http.GetStringAsync("http://localhost:9224/json"))!.AsArray().FirstOrDefault(t=>t?["type"]?.GetValue<string>()=="page")?["webSocketDebuggerUrl"]?.GetValue<string>();if(address!=null)break;}catch(HttpRequestException){}await Task.Delay(250);}
 if(address==null)throw new Exception("Browser debugging unavailable");
 using var socket=new ClientWebSocket();await socket.ConnectAsync(new Uri(address),CancellationToken.None);var cdp=new Cdp(socket);await cdp.Call("Page.enable",new{});await cdp.Call("Network.enable",new{});
 foreach(var cookie in JsonNode.Parse(File.ReadAllText(cookiePath))!.AsArray())await cdp.Call("Network.setCookie",new{name=cookie!["Name"]!.GetValue<string>(),value=cookie["Value"]!.GetValue<string>(),url=baseUrl,path="/",httpOnly=true});
 foreach(var mode in new[]{("desktop",1440,1050,false),("mobile",390,1000,true)}){
  await cdp.Call("Emulation.setDeviceMetricsOverride",new{width=mode.Item2,height=mode.Item3,deviceScaleFactor=1,mobile=mode.Item4});
  foreach(var page in new[]{("dashboard","/Dashboard"),("assistant","/Assistant"),("knowledge","/Knowledge")}){
   await cdp.Call("Page.navigate",new{url=baseUrl+page.Item2});await Task.Delay(700);
   var ready=await cdp.Call("Runtime.evaluate",new{expression="({width:innerWidth,scroll:document.documentElement.scrollWidth,error:document.body.innerText.includes('An unhandled exception')})",returnByValue=true});var value=ready.GetProperty("result").GetProperty("value");
   if(value.GetProperty("error").GetBoolean())throw new Exception("Server error on "+page.Item1);
   var overflow=value.GetProperty("scroll").GetInt32()>mode.Item2+1;
   if(page.Item1=="assistant"&&!mode.Item4){await cdp.Call("Runtime.evaluate",new{expression="document.querySelector('[data-prompt]').click()"});await Task.Delay(1000);var replied=await cdp.Call("Runtime.evaluate",new{expression="!document.querySelector('#assistant-send').disabled && document.querySelectorAll('#assistant-sources a').length > 0",returnByValue=true});if(!replied.GetProperty("result").GetProperty("value").GetBoolean())throw new Exception("Assistant UI request/source rendering failed");Console.WriteLine("PASS: assistant prompt sends request and renders answer sources");}
   var screenshot=await cdp.Call("Page.captureScreenshot",new{format="png",captureBeyondViewport=false});await File.WriteAllBytesAsync(Path.Combine(output,page.Item1+"-"+mode.Item1+".png"),Convert.FromBase64String(screenshot.GetProperty("data").GetString()!));
   if(overflow)throw new Exception("Horizontal overflow on "+mode.Item1+" "+page.Item1+": "+value);
   Console.WriteLine("PASS: "+page.Item1+" "+mode.Item1+" renders without overflow");
  }
 }
 await cdp.Call("Network.clearBrowserCookies",new{});await cdp.Call("Emulation.setDeviceMetricsOverride",new{width=390,height=1000,deviceScaleFactor=1,mobile=true});await cdp.Call("Page.navigate",new{url=baseUrl});await Task.Delay(700);
 var landing=await cdp.Call("Runtime.evaluate",new{expression="document.documentElement.scrollWidth <= innerWidth",returnByValue=true});var shot=await cdp.Call("Page.captureScreenshot",new{format="png"});await File.WriteAllBytesAsync(Path.Combine(output,"landing-mobile.png"),Convert.FromBase64String(shot.GetProperty("data").GetString()!));
 if(!landing.GetProperty("result").GetProperty("value").GetBoolean())throw new Exception("Mobile landing overflows");Console.WriteLine("PASS: public mobile landing without overflow");Console.WriteLine("All browser checks passed.");
}finally{if(!browser.HasExited)browser.Kill(true);File.Delete(cookiePath);}
sealed class Cdp(ClientWebSocket socket){
 private int id;
 public async Task<JsonElement> Call(string method,object parameters){var command=++id;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{id=command,method,@params=parameters}));await socket.SendAsync(bytes,WebSocketMessageType.Text,true,timeout.Token);var buffer=new byte[65536];
  while(true){using var stream=new MemoryStream();WebSocketReceiveResult chunk;do{chunk=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),timeout.Token);stream.Write(buffer,0,chunk.Count);}while(!chunk.EndOfMessage);using var doc=JsonDocument.Parse(stream.ToArray());var root=doc.RootElement;if(root.TryGetProperty("id",out var responseId)&&responseId.GetInt32()==command){if(root.TryGetProperty("error",out var error))throw new Exception(error.ToString());return root.GetProperty("result").Clone();}}
 }
}
