import type { ResearchRequest, SseEvent } from '../types/events'

/**
 * 以 POST + SSE 消费研究事件流。
 * 注意：EventSource 只支持 GET，axios 浏览器端拿不到流式响应体，
 * 所以用原生 fetch + ReadableStream 逐行解析——这是流式 POST 的行业标准做法。
 */
export async function streamResearch(
  request: ResearchRequest,
  onEvent: (evt: SseEvent) => void,
  signal: AbortSignal,
): Promise<void> {
  const response = await fetch('/api/research', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })
  if (!response.ok || !response.body) {
    throw new Error(`请求失败：HTTP ${response.status}`)
  }

  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()
  let buffer = ''
  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += value
    // SSE 帧以空行分隔；一帧内 event: 与 data: 各占一行
    let boundary: number
    while ((boundary = buffer.indexOf('\n\n')) >= 0) {
      const frame = buffer.slice(0, boundary)
      buffer = buffer.slice(boundary + 2)
      const eventName = frame.match(/^event: (.+)$/m)?.[1]
      const dataLine = frame.match(/^data: (.*)$/m)?.[1]
      if (eventName && dataLine) {
        onEvent({ type: eventName, ...JSON.parse(dataLine) } as SseEvent)
      }
    }
  }
}
