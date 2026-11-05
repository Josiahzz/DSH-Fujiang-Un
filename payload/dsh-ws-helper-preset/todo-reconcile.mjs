import { randomUUID } from 'node:crypto';

export const name = 'ws-helper-todo-reconcile';
export const inject = ['agentPresets'];

function currentTodos(agent) {
  let todos = null;
  for (const event of agent.session.ownEvents()) {
    if (event.type === 'turn/start') todos = null;
    if (event.type === 'todo/write') todos = event.data.todos;
  }
  return todos;
}

export function apply(ctx) {
  const checked = new Map();
  const progress = new Map();
  const enabled = agent => !agent.session.header.parentSession && ctx.agentPresets.composedPreset(agent.ctx) === 'ws-helper';
  ctx.on('agent/pre-step', async ({ agent, signal }, next) => {
    const decision = await next();
    if (decision.kind !== 'enter' || signal.aborted || !enabled(agent)) return decision;
    let state = progress.get(agent.id);
    if (!state) {
      state = { todos: currentTodos(agent), dirty: false, calls: new Map() };
      progress.set(agent.id, state);
    }
    if (!state.dirty || !state.todos?.some(row => row.status !== 'completed')) return decision;
    state.dirty = false;
    return { ...decision, messages: [...decision.messages, {
      id: randomUUID(), role: 'user', source: { kind: 'ws-helper-todo-progress' },
      content: [{ type: 'text', text: `[任务进度核对]\n当前完整清单：${JSON.stringify(state.todos)}\n刚才的工具结果已返回。先判断当前项的完成条件是否真正满足：满足就立即用 todo_write 标 completed，并把实际接着做的项目标 in_progress，再进入下一阶段；不要把状态更新攒到最终总结。若只完成部分调查或验证，继续当前项即可，不必为了提醒而重写清单。命令成功不等于整个任务完成，静态检查不等于实机通过。失败、缺条件或用户改目标时，按真实状态调整清单并说明影响；不要自动勾满，不扩大范围，不重复已完成操作。` }],
    }] };
  });
  ctx.on('session/event', (session, event) => {
    const state = progress.get(session.id);
    if (!state) return;
    if (event.type === 'turn/start') { state.todos = null; state.dirty = false; state.calls.clear(); }
    if (event.type === 'todo/write') { state.todos = event.data.todos; state.dirty = false; }
    if (event.type === 'tool/call') state.calls.set(event.data.callId, event.data.name);
    if (event.type === 'tool/result') {
      const id = event.data.message.toolCallId;
      const name = state.calls.get(id);
      state.calls.delete(id);
      if (name !== 'todo_write') state.dirty = true;
    }
  });
  ctx.on('agent/turn-stopping', ({ agent, signal, turn }) => {
    if (signal.aborted || !enabled(agent)) return;
    if (checked.get(agent.id) === turn) return;
    if ([...agent.inbox.nextStep, ...agent.inbox.nextTurn].some(row => row.source.kind === 'user')) return;
    const todos = currentTodos(agent);
    if (!todos?.some(row => row.status !== 'completed')) return;
    // One reconciliation per native turn, including when blockers remain.
    // A second incomplete list must never turn this into an endless work loop.
    checked.set(agent.id, turn);
    agent.steer({
      id: randomUUID(), role: 'user', source: { kind: 'ws-helper-todo-reconcile' },
      content: [{ type: 'text', text: `[任务清单收尾核对]\n你正在结束本轮，但当前清单还有未完成项：\n${JSON.stringify(todos)}\n这不是新任务，也不是要求继续扩大工作。请逐项对照本轮实际操作、产物和验证记录，用 todo_write 同步完整清单。已经有证据完成的项目才标 completed；未执行、失败、等待用户或设备的项目保留 pending/in_progress，并简短说明剩余工作与原因。静态检查、模拟测试不能冒充实机验证。不能只为进度显示100%而勾完，不能清空或删除未完成项来隐藏问题；仅在用户明确取消或更改范围时调整项目。若确有用户原要求尚未实现，应如实交代，不宣称全部完成。不要为了收尾再次执行已完成的外部操作。此前已输出总结时，不重复长总结，更新清单后只补一句收尾状态即可。` }],
    });
  });
  ctx.on('agent/disposed', ({ agent }) => { checked.delete(agent.id); progress.delete(agent.id); });
  ctx.on('dispose', () => { checked.clear(); progress.clear(); });
}
