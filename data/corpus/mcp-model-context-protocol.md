# MCP（Model Context Protocol）

## 定位

MCP（Model Context Protocol）是 Anthropic 于 2024 年提出并开放的协议，目标是成为"AI 应用连接外部能力的通用接口"——类似 USB-C 之于硬件：工具与数据源只需实现一次 MCP Server，任何兼容 MCP 的客户端（Claude 桌面版、各类 IDE、各 Agent 框架）都能直接使用，终结"每个框架各写一套工具集成"的 N×M 问题。

## 架构

MCP 采用 Client/Server 架构：

- **Host**：用户面对的 AI 应用（如 IDE、聊天应用）；
- **Client**：Host 内部的协议端，与每个 Server 维持一对一连接；
- **Server**：轻量进程，通过标准输入输出（本地）或 HTTP（远程）暴露能力。

通信基于 JSON-RPC，Server 可以独立开发、独立部署、独立授权。

## 三类原语

MCP Server 可暴露三种能力：

- **工具（Tools）**：可被模型调用的函数，如查数据库、发消息；
- **资源（Resources）**：可读取的数据，如文件内容、数据库记录，由应用方决定何时加载；
- **提示（Prompts）**：预置的提示词模板，供用户或应用选用。

## 与函数调用的关系

函数调用（Function Calling）是模型层面的能力：模型按约定格式输出"我要调用某函数"。MCP 是传输与生态层面的标准：规定这些函数如何被发现、描述、鉴权和执行。两者是互补关系——Harness 从 MCP Server 拉取工具列表，转换成模型可用的函数定义；模型发起函数调用后，Harness 再路由到对应 MCP Server 执行。在 Microsoft Agent Framework 中，MCP 工具可以直接包装为 Agent 可用的函数工具，接入框架自带的审批与遥测管道。
