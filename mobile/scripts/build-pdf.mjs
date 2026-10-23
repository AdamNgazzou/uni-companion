import { build } from 'esbuild';
import { writeFile } from 'node:fs/promises';
const bundle = await build({ stdin: { contents: `import * as pdfjs from 'pdfjs-dist/legacy/build/pdf.mjs'; import { WorkerMessageHandler } from 'pdfjs-dist/legacy/build/pdf.worker.mjs'; globalThis.pdfjsWorker={WorkerMessageHandler}; globalThis.uniPdf=pdfjs;`, resolveDir: process.cwd() }, bundle: true, write: false, format: 'iife', platform: 'browser', target: 'es2022', minify: true, legalComments: 'inline' });
const js = bundle.outputFiles[0].text.replaceAll('</script', '<\\/script');
const html = `<!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=5"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data: blob:; font-src data: blob:; connect-src 'none'; worker-src 'none';"><style>html{min-height:100%;}body{margin:0;background:#EDF2FC;overflow:auto;touch-action:pan-x pan-y;}canvas{display:block;margin:12px auto;max-width:none;}#status{font:16px sans-serif;padding:24px;color:#10153D}</style></head><body><p id="status">Opening your original PDF…</p><canvas id="page"></canvas><script>${js}</script><script>
let pdf=null,rendering=false,pending=null,currentPage=0,currentZoom=1,lastRequest=0;
function send(value){const message=JSON.stringify(value);if(window.ReactNativeWebView)window.ReactNativeWebView.postMessage(message);else parent.postMessage(message,'*')}
async function render(page,zoom,requestId){
 const args={page,zoom,requestId};if(rendering){pending=args;return}rendering=true;
 try{
  const n=Math.max(1,Math.min(pdf.numPages,Number(page)||1));const z=Number.isFinite(zoom)?Math.max(1,Math.min(3,zoom)):1;
  const p=await pdf.getPage(n),base=p.getViewport({scale:1});const viewport=p.getViewport({scale:Math.max(1,innerWidth-24)/base.width*z});
  const canvas=document.getElementById('page');const oldWidth=parseFloat(canvas.style.width)||viewport.width,oldHeight=parseFloat(canvas.style.height)||viewport.height;
  const centerX=(scrollX+innerWidth/2)/oldWidth,centerY=(scrollY+Math.min(innerHeight,oldHeight)/2)/oldHeight;
  const density=Math.min(devicePixelRatio||1,2,Math.sqrt(12000000/(viewport.width*viewport.height)));
  canvas.width=Math.max(1,Math.floor(viewport.width*density));canvas.height=Math.max(1,Math.floor(viewport.height*density));canvas.style.width=viewport.width+'px';canvas.style.height=viewport.height+'px';
  await p.render({canvasContext:canvas.getContext('2d'),viewport,transform:[density,0,0,density,0,0]}).promise;p.cleanup();
  document.getElementById('status').style.display='none';
  if(currentPage!==n)scrollTo(0,0);else scrollTo(Math.max(0,centerX*viewport.width-innerWidth/2),Math.max(0,centerY*viewport.height-Math.min(innerHeight,viewport.height)/2));
  currentPage=n;currentZoom=z;lastRequest=requestId;send({type:'page',page:n,total:pdf.numPages,zoom:z,requestId});
 }catch(e){send({type:'error',requestId})}finally{rendering=false;if(pending){const next=pending;pending=null;render(next.page,next.zoom,next.requestId)}}
}
async function receive(event){let message;try{message=typeof event.data==='string'?JSON.parse(event.data):event.data;if(message.type==='open'){if(pdf)await pdf.destroy();const data=Uint8Array.from(atob(message.data),c=>c.charCodeAt(0));pdf=await uniPdf.getDocument({data,enableScripting:false,isEvalSupported:false,isOffscreenCanvasSupported:false,disableFontFace:true,useSystemFonts:true,enableXfa:false,stopAtErrors:true,maxImageSize:12000000}).promise;await render(message.page,message.zoom,message.requestId)}else if(message.type==='page'&&pdf)await render(message.page,message.zoom,message.requestId)}catch(e){send({type:'error',requestId:message&&message.requestId})}}
window.addEventListener('message',receive);document.addEventListener('message',receive);let resizeTimer;window.addEventListener('resize',()=>{clearTimeout(resizeTimer);resizeTimer=setTimeout(()=>{if(pdf)render(currentPage,currentZoom,lastRequest)},120)});send({type:'ready'});
</script></body></html>`;
await writeFile('assets/pdf-viewer.html', html);
console.log(`Bundled offline PDF renderer: ${Math.round(html.length/1024)} KB`);
