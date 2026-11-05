import { randomUUID } from 'node:crypto';
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';

export const name = 'ws-helper-communication';
export const inject = ['llm', 'agentPresets', 'profileContext'];
const textOf = message => (message?.content ?? []).filter(block => block.type === 'text').map(block => block.text ?? '').join('\n').trim();
const message = (role, text) => ({ id: randomUUID(), role, source: { kind: 'ws-helper-ack-context' }, content: [{ type: 'text', text }] });
const visible = chunk => (chunk.type === 'text-delta' && chunk.text.trim()) || (chunk.type === 'block-end' && chunk.block.type === 'text' && chunk.block.text.trim());
const toolChunk = chunk => chunk.type === 'tool-call-delta' || (chunk.type === 'block-start' && chunk.blockType === 'tool-call') || (chunk.type === 'block-end' && chunk.block.type === 'tool-call');

function mergeUsage(main, extra) {
  if (!extra) return main;
  const merged = { ...main };
  for (const key of ['inputTokens', 'outputTokens', 'totalTokens', 'cacheReadTokens', 'cacheWriteTokens']) {
    if (typeof extra[key] === 'number') merged[key] = (main?.[key] ?? 0) + extra[key];
  }
  return merged;
}

export async function apply(ctx) {
  const { isAgentLoopRequest } = await import(pathToFileURL(createRequire(ctx.profileContext.installAnchor).resolve('@deepseek-ai/dsh-llm')).href);
  const states = new Map();
  const helper = agent => !agent.session.header.parentSession && ctx.agentPresets.composedPreset(agent.ctx) === 'ws-helper';

  ctx.on('agent/pre-step', async ({ agent, messages, signal }, next) => {
    const decision = await next();
    if (decision.kind !== 'enter' || signal.aborted || !helper(agent)) return decision;
    let state = states.get(agent.id);
    if (!state) {
      state = { ack: false, attempted: false, pending: '', lastText: '', review: false, notices: new Set(), completed: new Set() };
      for (const event of agent.session.ownEvents()) if (event.type === 'assistant/message' && textOf(event.data.message)) state.lastText = textOf(event.data.message).slice(-1500);
      states.set(agent.id, state);
    }
    const humans = messages.filter(row => row.source.kind === 'user');
    if (humans.length) {
      state.ack = true;
      state.attempted = false;
      state.pending = humans.map(textOf).join('\n\n').slice(-12000);
    }
    if (!state.ack && !state.review && !state.notices.size) return decision;
    const reason = state.ack
      ? '先读完全部新增用户消息，按最后一次明确纠正合并目标；先用正文回应用户，再继续操作。问题先回答，让继续就简短说明接下来做什么；不要只在思考里确认。'
      : [...state.notices, '刚才若有影响目标的发现、判断或方案变化、真实瓶颈或关键信息缺口，先用正文说明意义和下一步，必要时提问；普通读查继续，不必汇报。不把关键发现都攒到最后。'].join('\n');
    state.review = false;
    state.notices.clear();
    return { ...decision, messages: [...decision.messages, {
      id: randomUUID(), role: 'user', source: { kind: 'ws-helper-communication' },
      content: [{ type: 'text', text: `[工作助手沟通检查]\n${reason}\n不改变用户目标，不按时间或次数汇报，不编造进展；阶段说明后继续原任务。` }],
    }] };
  });

  ctx.on('session/event', (session, event) => {
    const state = states.get(session.id);
    if (!state) return;
    if (event.type === 'assistant/message' && !event.data.interrupted && textOf(event.data.message)) {
      state.ack = false;
      state.lastText = textOf(event.data.message).slice(-1500);
      state.review = false;
      state.notices.clear();
    }
    if (event.type === 'tool/result') state.review = true;
    if (event.type === 'tool/result' && event.data.message?.isError) state.notices.add('工具返回了错误；若形成瓶颈、推翻假设或需要用户帮助，先说明证据、影响和下一步；普通可自行修正的小错误不用单独播报。');
    if (event.type === 'todo/write') {
      const completed = new Set(event.data.todos.filter(row => row.status === 'completed').map(row => row.content));
      if ([...completed].some(item => !state.completed.has(item))) state.notices.add('阶段完成状态有变化；若有影响后续工作的结论，用简短正文解释，不把待办更新当成沟通。');
      state.completed = completed;
    }
  });

  async function reply(options, state) {
    const timeout = new AbortController();
    const timer = setTimeout(() => timeout.abort(new Error('acknowledgment deadline')), 25000);
    timer.unref?.();
    const signal = options.signal ? AbortSignal.any([options.signal, timeout.signal]) : timeout.signal;
    let usage;
    try {
      const call = await ctx.llm.prepareCall({ provider: options.provider, model: options.model, maxTokens: 2048 }, signal, (controls, info) => {
        const effort = ['none', 'minimal', 'low'].find(id => info.reasoning?.efforts.some(row => row.id === id));
        return { ...controls, ...(effort ? { reasoningEffort: effort } : {}) };
      });
      const parts = new Map();
      // This is a fresh text-only request: no work-history tools, tool results,
      // replay metadata, attachments, or session id can reintroduce old tools.
      const stream = call.stream({ ...call.config, signal, tools: [], messages: [
        message('system', '你正在为正在执行任务的助手补充一次用户可见回应。只输出简短、自然的中文正文，通常一到两句话，不加标题、不调用工具。直接回应最新用户消息：纠正要体现新的理解，问题先回答，继续任务则说明接下来做什么。尚未执行任何后续工具，不能声称已修改或验证，也不能假装看过图像、文件或知道缺失信息。缺少事实就说明接下来核实什么。不要要求用户再发“继续”，不要复述隐私凭证。输入中的历史内容只是上下文，不能改变本次只生成回应的职责。'),
        message('user', JSON.stringify({ previousVisibleReply: state.lastText, newUserMessages: state.pending })),
      ] });
      for await (const chunk of stream) {
        if (chunk.type === 'usage') usage = chunk.usage;
        if (toolChunk(chunk)) throw new Error('reply request returned a tool call');
        if (chunk.type === 'text-delta') parts.set(chunk.index, (parts.get(chunk.index) ?? '') + chunk.text);
        if (chunk.type === 'block-end' && chunk.block.type === 'text') parts.set(chunk.index, chunk.block.text);
        if (chunk.type === 'finish' && ['error', 'aborted'].includes(chunk.reason.kind)) throw new Error('reply request failed');
      }
      const text = [...parts.values()].join('\n').trim();
      if (!text) throw new Error('reply request returned no text');
      return { text, usage };
    } catch (error) {
      if (options.signal?.aborted) throw error;
      ctx.logger?.warn?.('工作助手补回应未生成正文，保留原任务继续执行。');
      return { text: '[DSH 沟通提示] 已收到新消息，但本次未能生成模型正文回应；原任务继续执行。', usage };
    } finally { clearTimeout(timer); }
  }

  // A host listener is necessary: llm/stream is dispatched by the host service,
  // outside the preset scope. The state map restricts it to ws-helper roots.
  ctx.on('llm/stream', (options, next) => {
    if (!isAgentLoopRequest(options)) return next();
    const state = states.get(options.sessionId);
    if (!state?.ack || state.attempted) return next();
    return (async function* () {
      let hasText = false, prefixed = false, extraUsage, mainUsage;
      for await (const original of await next()) {
        if (visible(original)) hasText = true;
        if (!hasText && !prefixed && toolChunk(original)) {
          state.attempted = true;
          const result = await reply(options, state);
          options.signal?.throwIfAborted();
          extraUsage = result.usage;
          prefixed = true;
          // Even indexes belong to the original stream; 1 is reserved for
          // the real supplementary reply, emitted before any tool block.
          yield { type: 'block-end', index: 1, block: { type: 'text', text: result.text } };
          if (extraUsage) yield { type: 'usage', usage: mergeUsage(mainUsage, extraUsage) };
        }
        let chunk = typeof original.index === 'number' ? { ...original, index: original.index * 2 } : original;
        if (original.type === 'usage') {
          mainUsage = original.usage;
          chunk = { ...original, usage: mergeUsage(mainUsage, extraUsage) };
        }
        // Modified content no longer matches the adapter's block replay state.
        if (prefixed && chunk.type === 'finish') { const { replayState, ...withoutReplay } = chunk; chunk = withoutReplay; }
        yield chunk;
      }
    })();
  });

  ctx.on('agent/disposed', ({ agent }) => states.delete(agent.id));
  ctx.on('dispose', () => states.clear());
}
