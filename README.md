# 🌐 NetGraph: A .NET Library for AI Agents and Agent Graphs

NetGraph is a .NET 8 library for building stateful, multi-actor applications with LLMs, used to create agent and multi-agent workflows. It's a .NET implementation inspired by [LangGraph](https://github.com/langchain-ai/langgraph).

## Features

- **Graph-Based Agents**: Create complex agent behaviors by defining workflows as directed graphs
- **State Management**: Built-in state management and persistence for maintaining context
- **Memory**: Store and retrieve information across user interactions
- **Human-in-the-Loop**: Support for checkpointing execution to allow for human intervention
- **Tool Integration**: Simple API for defining and using tools with your agents
- **LLM Integration**: Built-in support for OpenAI and extensible for other providers

## Installation

```shell
dotnet add package NetGraph