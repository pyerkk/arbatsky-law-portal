'use strict';
const {app,BrowserWindow,globalShortcut,ipcMain,Menu,Tray,nativeImage,protocol,shell,screen}=require('electron');
const fs=require('node:fs');const path=require('node:path');const os=require('node:os');const {DATA_FILES,validateSnapshot}=require('./database');
const isTest=process.argv.includes('--self-test');
if(isTest)app.setPath('userData',fs.mkdtempSync(path.join(os.tmpdir(),'arbatsky-map-test-')));
protocol.registerSchemesAsPrivileged([{scheme:'portal',privileges:{standard:true,secure:true,supportFetchAPI:true}}]);
const defaults={toggle:'Control+Shift+Space',pass:'Control+Shift+F2',opacity:0.96,alwaysOnTop:true,autostart:false,bounds:null};let settings={...defaults},window, tray,passing=false,quitting=false,snapshot=null;
const appPath=()=>path.join(app.getPath('userData'),'settings.json');const cachePath=()=>path.join(app.getPath('userData'),'site-data.json');
function loadSettings(){try{const p=JSON.parse(fs.readFileSync(appPath(),'utf8'));settings={...defaults,...p,opacity:Math.max(.4,Math.min(1,Number(p.opacity)||.96))};}catch{}}
function saveSettings(){fs.mkdirSync(app.getPath('userData'),{recursive:true});let tmp=appPath()+'.tmp';fs.writeFileSync(tmp,JSON.stringify(settings,null,2));fs.renameSync(tmp,appPath());}
function loadSnapshot(){try{const data=JSON.parse(fs.readFileSync(cachePath(),'utf8'));snapshot=validateSnapshot(data.files);return data.savedAt||null}catch{snapshot=null;return null}}
function registerKeys(toggle,pass){
 if(typeof toggle!=='string'||typeof pass!=='string'||toggle.length>80||pass.length>80||toggle.toLowerCase()===pass.toLowerCase())return false;
 globalShortcut.unregisterAll();let ok=false;
 try{ok=globalShortcut.register(toggle,toggleWindow)&&globalShortcut.register(pass,togglePass);}catch{}
 if(!ok){globalShortcut.unregisterAll();try{globalShortcut.register(settings.toggle,toggleWindow);globalShortcut.register(settings.pass,togglePass);}catch{}return false;}return true;
}
function toggleWindow(){if(!window)return;if(window.isVisible()){window.hide();}else{if(passing)setPass(false);window.show();window.focus();}}
function setPass(on){if(!window)return;passing=!!on;window.setIgnoreMouseEvents(passing,{forward:true});window.webContents.send('state',{passing});if(passing)window.blur();else if(window.isVisible())window.focus();}
function togglePass(){if(!window)return;if(!window.isVisible())window.showInactive();setPass(!passing);}
function setBounds(){if(!window||window.isMaximized()||window.isFullScreen())return;settings.bounds=window.getBounds();try{saveSettings()}catch{}}
async function updateData(){
 let candidate={};for(const file of DATA_FILES){let controller=new AbortController();let timer=setTimeout(()=>controller.abort(),15000);try{let r=await fetch('https://pyerkk.github.io/arbatsky-law-portal/'+file,{signal:controller.signal,redirect:'error'});if(!r.ok)throw Error('Ошибка загрузки '+file);candidate[file]=await r.text();}finally{clearTimeout(timer)}}
 validateSnapshot(candidate);let savedAt=new Date().toISOString(),target=cachePath(),temp=target+'.tmp';fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(temp,JSON.stringify({savedAt,files:candidate}));fs.renameSync(temp,target);snapshot=candidate;return savedAt;
}
function allowedSource(url){try{let u=new URL(url);return u.protocol==='https:'&&(u.hostname==='forum.russia.online'||(u.hostname==='github.com'&&u.pathname.startsWith('/pyerkk/arbatsky-law-portal/')))}catch{return false}}
function createWindow(){
 const displays=screen.getAllDisplays();let b=settings.bounds;let visible=b&&Number.isFinite(b.x)&&Number.isFinite(b.y)&&displays.some(d=>b.x>=d.bounds.x-200&&b.y>=d.bounds.y&&b.x<d.bounds.x+d.bounds.width-100&&b.y<d.bounds.y+d.bounds.height-100);
 window=new BrowserWindow({width:visible?Math.max(620,Math.min(2200,b.width)):1100,height:visible?Math.max(480,Math.min(1600,b.height)):780,...(visible?{x:b.x,y:b.y}:{}),minWidth:620,minHeight:480,frame:false,show:!isTest,backgroundColor:'#171b1a',title:'Правовой портал | Арбатский',icon:path.join(__dirname,'icon.png'),webPreferences:{preload:path.join(__dirname,'preload.js'),contextIsolation:true,nodeIntegration:false,sandbox:true}});
 window.setMenuBarVisibility(false);window.setAlwaysOnTop(settings.alwaysOnTop);window.setOpacity(settings.opacity);
 window.on('close',e=>{if(!quitting){e.preventDefault();window.hide()}});window.on('move',setBounds);window.on('resize',setBounds);
 window.webContents.setWindowOpenHandler(({url})=>{if(allowedSource(url))shell.openExternal(url);return {action:'deny'}});
 window.webContents.on('will-navigate',(e,url)=>{if(!url.startsWith('portal://app/')){e.preventDefault();if(allowedSource(url))shell.openExternal(url)}});
 window.loadURL('portal://app/index.html#quick');
}
function createTray(){tray=new Tray(nativeImage.createFromPath(path.join(__dirname,'icon.png')).resize({width:16,height:16}));tray.setToolTip('Правовой портал | Арбатский');tray.setContextMenu(Menu.buildFromTemplate([{label:'Открыть / скрыть',click:toggleWindow},{label:'Режим просмотра',click:togglePass},{label:'Выход',click:()=>{quitting=true;app.quit()}}]));tray.on('double-click',toggleWindow);}
async function selfTest(){try{
 await new Promise((resolve,reject)=>{if(!window.webContents.isLoading())return resolve();window.webContents.once('did-finish-load',resolve);window.webContents.once('did-fail-load',(_,code,desc)=>reject(Error(code+' '+desc)));});
 const result=await window.webContents.executeJavaScript(`(async()=>{for(let i=0;i<100;i++){if(document.querySelectorAll('[data-graph-node]').length>0&&document.querySelector('#desk-full'))break;await new Promise(r=>setTimeout(r,100));}const check=(v,m)=>{if(!v)throw Error(m)};check(document.querySelectorAll('[data-graph-node]').length>5,'карта не загрузилась');check(document.querySelector('#desk-full'),'панель не появилась');document.querySelector('#node-labels').value='name';document.querySelector('#node-labels').dispatchEvent(new Event('change',{bubbles:true}));check(document.querySelector('.node-name'),'подписи не появились');document.querySelector('#add-note').click();check(document.querySelector('#note-dialog').open,'окно заметки не открылось');document.querySelector('#note-text').value='Проверочная заметка';document.querySelector('#note-form').requestSubmit();check([...document.querySelectorAll('[data-graph-note]')].some(n=>n.textContent.includes('Проверочная заметка')),'заметка не появилась');check(localStorage.getItem('arb-graph-profiles-v2')?.includes('Проверочная заметка'),'заметка не сохранена');document.querySelector('#desk-full').click();await new Promise(r=>setTimeout(r,500));check(document.body.classList.contains('desktop-focus'),'полноэкранная карта не открылась');document.querySelector('#desk-full').click();await new Promise(r=>setTimeout(r,500));check(!document.body.classList.contains('desktop-focus'),'полноэкранная карта не закрылась');return 'ok'})()`);
 if(result!=='ok')throw Error('Неизвестный результат');
 window.show();window.focus();
 await window.webContents.executeJavaScript(`document.querySelector('#desk-full').click()`);
 await new Promise(r=>setTimeout(r,800));
 let before=await window.webContents.executeJavaScript(`(()=>{let n=document.querySelector('[data-graph-node="p317-"]'),v=document.querySelector('#graph-viewport'),s=document.querySelector('#graph-stage'),r=n.getBoundingClientRect(),vr=v.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2,target:vr.right-120,left:parseFloat(n.style.left),width:parseFloat(s.style.width),screen:screen.width}})()`);
 if(before.width<before.screen)throw Error('Поле карты уже экрана');
 if(before.target-before.x<100)throw Error('Недостаточно места для проверки перемещения');
 const input=window.webContents;
 input.sendInputEvent({type:'mouseMove',x:Math.round(before.x),y:Math.round(before.y)});
 input.sendInputEvent({type:'mouseDown',x:Math.round(before.x),y:Math.round(before.y),button:'left',clickCount:1});
 input.sendInputEvent({type:'mouseMove',x:Math.round(before.target),y:Math.round(before.y),button:'left'});
 input.sendInputEvent({type:'mouseUp',x:Math.round(before.target),y:Math.round(before.y),button:'left',clickCount:1});
 await new Promise(r=>setTimeout(r,250));
 let moved=await window.webContents.executeJavaScript(`parseFloat(document.querySelector('[data-graph-node="p317-"]').style.left)`);
 if(moved-before.left<100)throw Error('Не удалось перетащить точку по ширине карты');
 let group=await window.webContents.executeJavaScript(`(()=>{let g=document.querySelector('[data-graph-group="officer"]'),c=document.querySelector('[data-graph-node="p318-1"]'),r=g.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2,left:parseFloat(g.style.left),child:parseFloat(c.style.left)}})()`);
 input.sendInputEvent({type:'mouseMove',x:Math.round(group.x),y:Math.round(group.y)});
 input.sendInputEvent({type:'mouseDown',x:Math.round(group.x),y:Math.round(group.y),button:'left',clickCount:1});
 input.sendInputEvent({type:'mouseMove',x:Math.round(group.x-100),y:Math.round(group.y),button:'left'});
 input.sendInputEvent({type:'mouseUp',x:Math.round(group.x-100),y:Math.round(group.y),button:'left',clickCount:1});
 await new Promise(r=>setTimeout(r,250));
 let afterGroup=await window.webContents.executeJavaScript(`({left:parseFloat(document.querySelector('[data-graph-group="officer"]').style.left),child:parseFloat(document.querySelector('[data-graph-node="p318-1"]').style.left)})`);
 if(group.left-afterGroup.left<20||Math.abs((group.left-afterGroup.left)-(group.child-afterGroup.child))>1)throw Error('Категория не переместилась вместе со статьями');
 moved=await window.webContents.executeJavaScript(`parseFloat(document.querySelector('[data-graph-node="p317-"]').style.left)`);
 await window.webContents.executeJavaScript(`document.querySelector('#desk-full').click()`);
 await new Promise(r=>setTimeout(r,500));
 let after=await window.webContents.executeJavaScript(`parseFloat(document.querySelector('[data-graph-node="p317-"]').style.left)`);
 if(Math.abs(after-moved)>1)throw Error('Координата точки изменилась при выходе из карты');
 app.exit(0);
 }catch(e){try{fs.writeFileSync(path.join(os.tmpdir(),'arbatsky-selftest-error.txt'),String(e.stack||e))}catch{}app.exit(1)}}
app.whenReady().then(()=>{
 loadSettings();let updated=loadSnapshot();
 protocol.handle('portal',request=>{const u=new URL(request.url);if(u.hostname!=='app')return new Response('Not found',{status:404});let name=decodeURIComponent(u.pathname).replace(/^\/+/, '');if(!/^[a-z0-9_.-]+$/i.test(name))return new Response('Forbidden',{status:403});try{let data=snapshot&&DATA_FILES.includes(name)?snapshot[name]:fs.readFileSync(name==='desktop.css'?path.join(__dirname,name):path.join(__dirname,'web',name));let ext=path.extname(name);let types={'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.json':'application/json; charset=utf-8'};return new Response(data,{headers:{'Content-Type':types[ext]||'application/octet-stream','Cache-Control':'no-store'}})}catch{return new Response('Not found',{status:404})}});
 createWindow();if(!isTest){createTray();if(!registerKeys(settings.toggle,settings.pass))window.webContents.once('did-finish-load',()=>window.webContents.send('error','Не удалось зарегистрировать горячие клавиши. Измените их в настройках.'));if(process.argv.includes('--background'))window.hide();}else selfTest();
 ipcMain.handle('desktop:get',()=>({settings,passing,updated}));
 ipcMain.handle('desktop:action',async(_event,action,payload)=>{
  if(action==='hide'){toggleWindow();return true} if(action==='pass'){togglePass();return passing}
  if(action==='maximize'){window.setFullScreen(!window.isFullScreen());return window.isFullScreen()}
  if(action==='update'){updated=await updateData();return updated}
  if(action==='settings'){
   if(!payload||typeof payload!=='object')throw Error('Некорректные настройки');let toggle=String(payload.toggle||'').trim(),pass=String(payload.pass||'').trim();
   if(!isTest&&!registerKeys(toggle,pass))throw Error('Горячие клавиши заняты или заданы неверно');
   settings={...settings,toggle,pass,opacity:Math.max(.4,Math.min(1,Number(payload.opacity)||.96)),alwaysOnTop:!!payload.alwaysOnTop,autostart:!!payload.autostart};window.setOpacity(settings.opacity);window.setAlwaysOnTop(settings.alwaysOnTop);
   if(app.isPackaged)app.setLoginItemSettings({openAtLogin:settings.autostart,args:['--background']});saveSettings();return settings;
  }throw Error('Неизвестная команда');
 });
});
app.on('window-all-closed',()=>{});app.on('before-quit',()=>{quitting=true;globalShortcut.unregisterAll();});
