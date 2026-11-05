import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {setTimeout as delay} from 'node:timers/promises';
const task=path.dirname(fileURLToPath(import.meta.url));
const runtime=process.env.DSH_TEST_RUNTIME || path.join(process.env.LOCALAPPDATA,'Programs/DeepSeek Harness/resources/app.asar/dsh');
async function pkg(name){const d=runtime+'/node_modules/@deepseek-ai/'+name;const m=JSON.parse(await fs.readFile(d+'/package.json','utf8'));return import(pathToFileURL(path.join(d,m.exports?.['.']?.default??m.main)).href);}
const boot=await pkg('dsh-app-boot'),cmdline=await pkg('dsh-cmdline');
const {SessionId}=await pkg('dsh-session');
const {LlmAdapter,createUserMessage}=await pkg('dsh-llm');
const {defineTool}=await pkg('dsh-tools');
const home=path.resolve(task,'../outputs/runtime-qa'),dir=home+'/profiles/spec',cwd=home+'/workspace';
await fs.mkdir(dir+'/node_modules/@local',{recursive:true});await fs.mkdir(cwd,{recursive:true});
try{await fs.symlink(path.resolve(task,'../payload/dsh-ws-helper-preset'),dir+'/node_modules/@local/dsh-ws-helper-preset','junction');}catch(e){if(e.code!=='EEXIST')throw e;}
boot.initProfile(dir,['@deepseek-ai/dsh-base','@deepseek-ai/dsh-web-app','@local/dsh-ws-helper-preset']);
const anchor=runtime+'/node_modules/@deepseek-ai/dsh/package.json';
const profile={name:'spec',dir,layers:[],patchPath:dir+'/cordis.patch.yml',patches:[],skippedBundles:[]};
const resolution=await boot.createRuntimeResolution({installAnchor:anchor,home,profile});
const patches=[];
for(const d of [runtime+'/node_modules/@deepseek-ai/dsh-base',runtime+'/node_modules/@deepseek-ai/dsh-web-app',path.resolve(task,'../payload/dsh-ws-helper-preset')]){const m=JSON.parse(await fs.readFile(d+'/package.json','utf8'));for(const f of boot.bundlePatchPaths(d,m.dsh.bundle))patches.push(...boot.loadOverlayPatches('communication-qa',f));}
const overrides=[{id:'storage-json',config:{root:home+'/storages'}},{id:'session-persistence-jsonl',config:{root:home+'/sessions'}},{id:'webserver',inject:[],config:{host:'127.0.0.1',port:0}},...['web-runtime','session-telemetry-otel','modules','open-in-app','session-log-download','client-hmr','hmr','directory-picker'].map(id=>({id,disabled:true})),{id:'connection',inject:['webServer'],config:{trustedHosts:[]}},{id:'agent-preset-registry',config:{default:'standard'}}];
await fs.writeFile(dir+'/cordis.yml','[]\n');
const ctx=await boot.boot('communication-qa',dir+'/cordis.yml',[...patches,...overrides],async b=>{b.provide('profileContext',{name:'spec',dir,patchPath:profile.patchPath,installAnchor:anchor,home,cwd,startedBundles:['@deepseek-ai/dsh-base','@deepseek-ai/dsh-web-app','@local/dsh-ws-helper-preset'],overlays:overrides,telemetryDisabledEnv:'1'});await b.plugin(boot.PluginPackages,{resolution});cmdline.provideCmdline(b,{args:[],exit:()=>{}});});


const text=m=>(m?.content??[]).filter(b=>b.type==='text').map(b=>b.text).join('\n').trim();
const user=s=>createUserMessage({source:{kind:'user'},content:[{type:'text',text:s}]});
const done=[{content:'检查样本',status:'completed'},{content:'实现变更',status:'completed'},{content:'验证结果',status:'completed'}];
const stale=[{content:'检查样本',status:'completed'},{content:'实现变更',status:'in_progress'},{content:'验证结果',status:'pending'}];
const blocked=[...done.slice(0,2),{content:'手机实机验证',status:'pending'}];
const executed=[];let ready;
class Adapter extends LlmAdapter{
 async *stream(o){
  const label=o.messages.filter(m=>m.role==='user'&&m.source.kind==='user').map(text).at(-1)||'';
  const rec=o.messages.filter(m=>m.source?.kind==='ws-helper-todo-reconcile').length;
  const results=o.messages.filter(m=>m.role==='tool');const has=id=>results.some(m=>m.toolCallId===id);
  const call=(id,name,args)=>({type:'block-end',index:1,block:{type:'tool-call',id,name,arguments:JSON.stringify(args)}});
  if(label==='CASE:list'&&!has('list')){yield {type:'text-delta',index:0,text:'检查子代理列表。'};yield call('list','list_agents',{});yield {type:'usage',usage:{inputTokens:1,outputTokens:1,totalTokens:2,cacheReadTokens:0,cacheWriteTokens:0}};yield {type:'finish',reason:{kind:'tool-calls'}};return;}
  if(label==='CASE:list'){assert(!results.at(-1).isError);yield {type:'text-delta',index:0,text:'子代理列表可用。'};yield {type:'finish',reason:{kind:'stop'}};return;}
  if(label==='CASE:question'){yield {type:'text-delta',index:0,text:'回答问题，不新增任务。'};yield {type:'finish',reason:{kind:'stop'}};return;}
  if(rec){
   if(label==='CASE:ignore'){yield {type:'text-delta',index:0,text:'还有未完成项目，本次不继续工作。'};yield {type:'finish',reason:{kind:'stop'}};return;}
   if(!has('reconciled')){yield {type:'text-delta',index:0,text:'按实际证据同步清单。'};yield call('reconciled','todo_write',{todos:label==='CASE:blocked'?blocked:done});yield {type:'finish',reason:{kind:'tool-calls'}};return;}
   yield {type:'text-delta',index:0,text:label==='CASE:blocked'?'实现和静态检查已完成；手机实机验证待用户提供设备，未完成。':'清单已同步，三项均有夹具证据。'};yield {type:'finish',reason:{kind:'stop'}};return;
  }
  if(!has('plan')){yield {type:'text-delta',index:0,text:'先记录测试清单，再执行夹具。'};yield call('plan','todo_write',{todos:stale});yield {type:'finish',reason:{kind:'tool-calls'}};return;}
  if(!has('work')){yield call('work','qa_work',{});yield {type:'finish',reason:{kind:'tool-calls'}};return;}
  if(label==='CASE:complete'&&!has('closed')){yield call('closed','todo_write',{todos:done});yield {type:'finish',reason:{kind:'tool-calls'}};return;}
  yield {type:'text-delta',index:0,text:'实现和夹具验证结束。'};yield {type:'finish',reason:{kind:'stop'}};
 }
}
ctx.llm.registerAdapter(['todo-qa'],new Adapter());
async function make(label,preset='ws-helper'){
 const h=await ctx.agents.create({sessionId:SessionId('todo-'+label+'-'+Date.now()),meta:{cwd},agentOptions:{provider:'todo-qa',model:'test'},setup:async a=>{
  await ctx.agentPresets.mount(a,preset);
  a.tools.register(defineTool({name:'qa_work',description:'Execute an isolated bookkeeping fixture.',parameters:{},output:{schema:{type:'string'},render:(_a,v)=>[{type:'text',text:v}]},execute:async(_args,exec)=>{executed.push(exec.agent.id);if(label==='cancel'){ready?.();await delay(5000,undefined,{signal:exec.signal});}return '夹具证据：样本检查、实现、静态测试均已通过。没有手机设备，未做实机测试。';}}));
 }});
 const assembly=await ctx.systemPrompt.assemble({agent:h.agent,scope:h.agent});
 assert.equal(assembly.tools.filter(t=>t.name==='list_agents').length,1);
 if(preset==='ws-helper')for(const tool of ['subagent','subagent_fork','send_message','interrupt_agent'])assert(assembly.tools.some(t=>t.name===tool),'Missing '+tool);
 return h;
}
async function settle(a){let timer;try{await Promise.race([a.whenIdle(),new Promise((_,reject)=>timer=setTimeout(()=>reject(Error('timeout')),12000))]);}finally{clearTimeout(timer);}const errors=[...a.session.ownEvents()].filter(e=>e.type==='turn/end'&&e.data.reason.kind==='error');assert.equal(errors.length,0,JSON.stringify(errors));}
const report={runtime:'0.2.2-rc.1',networkModelCalls:0,checks:[]};
try{
 const listing=await make('list');try{listing.agent.followup(user('CASE:list'));await settle(listing.agent);report.checks.push('new-session-tool-catalog-and-list-agents-execution');}finally{await listing.dispose();}
 for(const label of ['stale','blocked','ignore','complete']){const h=await make(label);try{h.agent.followup(user('CASE:'+label));await settle(h.agent);const events=[...h.agent.session.ownEvents()];const reminders=events.filter(e=>e.type==='user/message'&&e.data.source.kind==='ws-helper-todo-reconcile');assert.equal(reminders.length,label==='complete'?0:1);assert.equal(executed.filter(id=>id===h.agent.id).length,1,'Do not repeat actual work');const todos=events.filter(e=>e.type==='todo/write').at(-1).data.todos;if(label==='stale')assert(todos.every(t=>t.status==='completed'));if(label==='blocked')assert.equal(todos.at(-1).status,'pending');if(label==='ignore')assert(todos.some(t=>t.status!=='completed'));report.checks.push(label+': truth-preserved-one-reconciliation-no-work-repeat');if(label==='blocked'){h.agent.followup(user('CASE:question'));await settle(h.agent);assert.equal([...h.agent.session.ownEvents()].filter(e=>e.type==='user/message'&&e.data.source.kind==='ws-helper-todo-reconcile').length,1);report.checks.push('previous-turn-stale-list-does-not-reopen-new-question');}}finally{await h.dispose();}}
 const plain=await make('standard','standard');try{plain.agent.followup(user('CASE:stale'));await settle(plain.agent);assert(![...plain.agent.session.ownEvents()].some(e=>e.type==='user/message'&&e.data.source.kind==='ws-helper-todo-reconcile'));report.checks.push('standard-preset-unaffected');}finally{await plain.dispose();}
 const canceled=await make('cancel');try{const started=new Promise(r=>ready=r);canceled.agent.followup(user('CASE:cancel'));await started;canceled.agent.cancel({kind:'user'});await canceled.agent.whenIdle();assert(![...canceled.agent.session.ownEvents()].some(e=>e.type==='user/message'&&e.data.source.kind==='ws-helper-todo-reconcile'));report.checks.push('user-stop-does-not-trigger-cleanup-work');}finally{await canceled.dispose();}
 await fs.writeFile(path.resolve(task,'../outputs/runtime-verification.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
}finally{await ctx.fiber.dispose();}
