# Comprehensive Guide: Integrating Claude Code with Ollama for Seamless AI-Powered Development

## Executive Summary

This comprehensive guide provides step-by-step instructions for creating a sophisticated integration between Claude Code and Ollama, enabling Claude Code to function as your primary coding agent while seamlessly leveraging Ollama's capabilities for research, error analysis, and architectural guidance. The integration includes full web connectivity, automated system updates, and transparent communication between the two AI systems.

## Architecture Overview

The integration creates a unified development environment where:

- **Claude Code** serves as your primary coding interface with built-in web search capabilities[^1][^2]
- **Ollama** provides specialized AI assistance through enhanced web connectivity and research tools[^3][^4]
- **Enhanced MCP Server** bridges the gap between Claude Code's native capabilities and Ollama's specialized functions[^5][^6]
- **Automated Workflows** ensure seamless, transparent operation with minimal manual intervention[^7][^8]


## Prerequisites

Before beginning the integration, ensure you have:

- **Claude Code**: Installed via `npm install -g @anthropic-ai/claude-code`[^1]
- **Ollama**: Installed with desired models running locally[^3]
- **Python 3.10+**: For creating MCP servers and integration scripts[^9][^10]
- **Node.js**: For running Claude Code and managing dependencies[^1]
- **Docker**: For running SearXNG and other containerized services[^11][^12]


## Step 1: Install Web Search Dependencies

First, set up the web search infrastructure that will give Ollama internet connectivity:

```bash
# Install SearXNG for privacy-focused web search
docker run -d \
  --name searxng \
  -p 8080:8080 \
  -v "${PWD}/searxng:/etc/searxng" \
  -e "BASE_URL=http://localhost:8080/" \
  -e "INSTANCE_NAME=my-searxng" \
  searxng/searxng:latest

# Install web search and scraping libraries
pip install requests beautifulsoup4 lxml httpx aiohttp duckduckgo-search
pip install selenium webdriver-manager playwright
pip install packaging requests-cache mcp
```


## Step 2: Enhanced Ollama MCP Server with Full Web Connectivity

Create the enhanced MCP server that provides Ollama with comprehensive web search, scraping, and update capabilities:

```python
# enhanced_ollama_mcp_server.py
import asyncio
import json
import logging
import subprocess
import os
import sys
from typing import Any, Dict, List, Optional, Union
import httpx
import requests
from bs4 import BeautifulSoup
from duckduckgo_search import DDGS
import aiohttp
from mcp import server, types
from mcp.server import NotificationOptions, Server
from mcp.server.models import InitializationOptions
from packaging import version
import time
from urllib.parse import urljoin, urlparse

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class EnhancedOllamaMCPServer:
    def __init__(self):
        self.ollama_url = "http://localhost:11434"
        self.searxng_url = "http://localhost:8080"
        self.client = httpx.AsyncClient(timeout=300.0)
        self.server = Server("enhanced-ollama-mcp")
        self.search_cache = {}
        self.update_cache = {}
        
    async def query_ollama(self, prompt: str, model: str = "llama3.2", context: str = "") -> str:
        """Query Ollama with context"""
        try:
            full_prompt = f"{context}\n\n{prompt}" if context else prompt
            
            payload = {
                "model": model,
                "prompt": full_prompt,
                "stream": False,
                "options": {
                    "temperature": 0.7,
                    "top_p": 0.9
                }
            }
            
            response = await self.client.post(
                f"{self.ollama_url}/api/generate",
                json=payload
            )
            
            if response.status_code == 200:
                result = response.json()
                return result.get("response", "")
            else:
                return f"Error: {response.status_code} - {response.text}"
                
        except Exception as e:
            logger.error(f"Error querying Ollama: {e}")
            return f"Error querying Ollama: {str(e)}"
    
    async def web_search(self, query: str, num_results: int = 5, search_engine: str = "auto") -> str:
        """Perform web search using multiple search engines"""
        try:
            results = []
            
            # Try SearXNG first if available
            if search_engine in ["auto", "searxng"]:
                try:
                    searxng_results = await self._search_searxng(query, num_results)
                    if searxng_results:
                        results.extend(searxng_results)
                except Exception as e:
                    logger.warning(f"SearXNG search failed: {e}")
            
            # Fallback to DuckDuckGo
            if not results or search_engine == "duckduckgo":
                try:
                    ddg_results = await self._search_duckduckgo(query, num_results)
                    results.extend(ddg_results)
                except Exception as e:
                    logger.warning(f"DuckDuckGo search failed: {e}")
            
            # Format results
            if results:
                formatted_results = "Web Search Results:\n\n"
                for i, result in enumerate(results[:num_results], 1):
                    formatted_results += f"{i}. **{result.get('title', 'No Title')}**\n"
                    formatted_results += f"   URL: {result.get('url', 'No URL')}\n"
                    formatted_results += f"   Summary: {result.get('content', 'No content available')}\n\n"
                
                return formatted_results
            else:
                return "No search results found."
                
        except Exception as e:
            logger.error(f"Error in web search: {e}")
            return f"Error performing web search: {str(e)}"
    
    async def _search_searxng(self, query: str, num_results: int) -> List[Dict]:
        """Search using SearXNG"""
        try:
            search_url = f"{self.searxng_url}/search"
            params = {
                "q": query,
                "format": "json",
                "category": "general"
            }
            
            response = await self.client.get(search_url, params=params)
            if response.status_code == 200:
                data = response.json()
                results = []
                
                for result in data.get("results", [])[:num_results]:
                    results.append({
                        "title": result.get("title", ""),
                        "url": result.get("url", ""),
                        "content": result.get("content", "")
                    })
                
                return results
            return []
        except Exception as e:
            logger.error(f"SearXNG search error: {e}")
            return []
    
    async def _search_duckduckgo(self, query: str, num_results: int) -> List[Dict]:
        """Search using DuckDuckGo"""
        try:
            ddgs = DDGS()
            results = []
            
            for result in ddgs.text(query, max_results=num_results):
                results.append({
                    "title": result.get("title", ""),
                    "url": result.get("href", ""),
                    "content": result.get("body", "")
                })
            
            return results
        except Exception as e:
            logger.error(f"DuckDuckGo search error: {e}")
            return []
    
    async def scrape_website(self, url: str, extract_type: str = "text") -> str:
        """Scrape website content"""
        try:
            headers = {
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36'
            }
            
            response = await self.client.get(url, headers=headers)
            if response.status_code == 200:
                soup = BeautifulSoup(response.text, 'html.parser')
                
                if extract_type == "text":
                    # Extract clean text
                    for script in soup(["script", "style"]):
                        script.decompose()
                    text = soup.get_text()
                    lines = (line.strip() for line in text.splitlines())
                    chunks = (phrase.strip() for line in lines for phrase in line.split("  "))
                    text = '\n'.join(chunk for chunk in chunks if chunk)
                    return text[:5000]  # Limit to 5000 characters
                
                elif extract_type == "links":
                    # Extract links
                    links = []
                    for link in soup.find_all('a', href=True):
                        href = link['href']
                        if href.startswith('http'):
                            links.append(f"{link.get_text(strip=True)}: {href}")
                    return '\n'.join(links[:20])  # Limit to 20 links
                
                else:
                    return response.text[:5000]  # Raw HTML
            
            return f"Error: Could not access {url} (Status: {response.status_code})"
            
        except Exception as e:
            logger.error(f"Error scraping website: {e}")
            return f"Error scraping website: {str(e)}"
    
    async def check_ollama_updates(self) -> str:
        """Check for Ollama updates"""
        try:
            # Get current version
            current_version = await self._get_ollama_version()
            
            # Check for updates
            latest_version = await self._get_latest_ollama_version()
            
            if current_version and latest_version:
                if version.parse(latest_version) > version.parse(current_version):
                    return f"Update available! Current: {current_version}, Latest: {latest_version}"
                else:
                    return f"Ollama is up to date (v{current_version})"
            
            return "Could not check for updates"
            
        except Exception as e:
            logger.error(f"Error checking updates: {e}")
            return f"Error checking updates: {str(e)}"
    
    async def _get_ollama_version(self) -> str:
        """Get current Ollama version"""
        try:
            response = await self.client.get(f"{self.ollama_url}/api/version")
            if response.status_code == 200:
                data = response.json()
                return data.get("version", "unknown")
            return "unknown"
        except Exception as e:
            logger.error(f"Error getting Ollama version: {e}")
            return "unknown"
    
    async def _get_latest_ollama_version(self) -> str:
        """Get latest Ollama version from GitHub"""
        try:
            github_url = "https://api.github.com/repos/ollama/ollama/releases/latest"
            response = await self.client.get(github_url)
            if response.status_code == 200:
                data = response.json()
                return data.get("tag_name", "").replace("v", "")
            return "unknown"
        except Exception as e:
            logger.error(f"Error getting latest version: {e}")
            return "unknown"
    
    async def update_ollama(self) -> str:
        """Update Ollama to latest version"""
        try:
            import platform
            system = platform.system().lower()
            
            if system == "linux":
                # Linux update command
                cmd = "curl -fsSL https://ollama.com/install.sh | sh"
                result = subprocess.run(cmd, shell=True, capture_output=True, text=True)
                
                if result.returncode == 0:
                    return f"Ollama updated successfully!\n{result.stdout}"
                else:
                    return f"Update failed: {result.stderr}"
            
            elif system == "darwin":  # macOS
                return "Please update Ollama manually on macOS by downloading from https://ollama.com/download"
            
            elif system == "windows":
                return "Please update Ollama manually on Windows by downloading from https://ollama.com/download"
            
            else:
                return f"Unsupported system: {system}"
                
        except Exception as e:
            logger.error(f"Error updating Ollama: {e}")
            return f"Error updating Ollama: {str(e)}"
    
    async def pull_model(self, model_name: str) -> str:
        """Pull a new model from Ollama registry"""
        try:
            payload = {"name": model_name}
            response = await self.client.post(f"{self.ollama_url}/api/pull", json=payload)
            
            if response.status_code == 200:
                return f"Successfully pulled model: {model_name}"
            else:
                return f"Error pulling model: {response.status_code} - {response.text}"
                
        except Exception as e:
            logger.error(f"Error pulling model: {e}")
            return f"Error pulling model: {str(e)}"
    
    async def research_with_web(self, topic: str, model: str = "llama3.2") -> str:
        """Perform comprehensive research using web search and Ollama"""
        try:
            # Step 1: Generate search queries
            search_prompt = f"""
            Generate 3-5 specific search queries to research this topic comprehensively: {topic}
            
            Provide only the search queries, one per line, without numbering or explanations.
            """
            
            queries_response = await self.query_ollama(search_prompt, model)
            search_queries = [q.strip() for q in queries_response.split('\n') if q.strip()]
            
            # Step 2: Perform web searches
            all_results = []
            for query in search_queries[:3]:  # Limit to 3 queries
                results = await self.web_search(query, num_results=3)
                all_results.append(f"Search Query: {query}\n{results}")
            
            # Step 3: Analyze and synthesize
            research_context = "\n\n".join(all_results)
            
            analysis_prompt = f"""
            Based on the following web search results, provide a comprehensive analysis of: {topic}
            
            Web Search Results:
            {research_context}
            
            Please provide:
            1. Key findings and insights
            2. Current trends and developments
            3. Important facts and statistics
            4. Reliable sources and references
            5. Conclusions and recommendations
            """
            
            final_analysis = await self.query_ollama(analysis_prompt, model)
            
            return f"Research Analysis for: {topic}\n\n{final_analysis}"
            
        except Exception as e:
            logger.error(f"Error in web research: {e}")
            return f"Error performing web research: {str(e)}"

# Initialize the enhanced server
mcp_server = EnhancedOllamaMCPServer()

@mcp_server.server.list_tools()
async def handle_list_tools() -> List[types.Tool]:
    """List available tools"""
    return [
        types.Tool(
            name="query_ollama",
            description="Query Ollama with a prompt and optional context",
            inputSchema={
                "type": "object",
                "properties": {
                    "prompt": {"type": "string", "description": "The prompt to send to Ollama"},
                    "model": {"type": "string", "description": "Ollama model to use", "default": "llama3.2"},
                    "context": {"type": "string", "description": "Additional context for the query"}
                },
                "required": ["prompt"]
            }
        ),
        types.Tool(
            name="web_search",
            description="Search the web using multiple search engines",
            inputSchema={
                "type": "object",
                "properties": {
                    "query": {"type": "string", "description": "Search query"},
                    "num_results": {"type": "integer", "description": "Number of results to return", "default": 5},
                    "search_engine": {"type": "string", "description": "Search engine to use", "default": "auto"}
                },
                "required": ["query"]
            }
        ),
        types.Tool(
            name="scrape_website",
            description="Scrape content from a website",
            inputSchema={
                "type": "object",
                "properties": {
                    "url": {"type": "string", "description": "URL to scrape"},
                    "extract_type": {"type": "string", "description": "Type of content to extract", "default": "text"}
                },
                "required": ["url"]
            }
        ),
        types.Tool(
            name="research_with_web",
            description="Perform comprehensive research using web search and AI analysis",
            inputSchema={
                "type": "object",
                "properties": {
                    "topic": {"type": "string", "description": "Topic to research"},
                    "model": {"type": "string", "description": "Ollama model to use", "default": "llama3.2"}
                },
                "required": ["topic"]
            }
        ),
        types.Tool(
            name="check_ollama_updates",
            description="Check for Ollama updates",
            inputSchema={
                "type": "object",
                "properties": {}
            }
        ),
        types.Tool(
            name="update_ollama",
            description="Update Ollama to the latest version",
            inputSchema={
                "type": "object",
                "properties": {}
            }
        ),
        types.Tool(
            name="pull_model",
            description="Pull a new model from Ollama registry",
            inputSchema={
                "type": "object",
                "properties": {
                    "model_name": {"type": "string", "description": "Name of the model to pull"}
                },
                "required": ["model_name"]
            }
        )
    ]

@mcp_server.server.call_tool()
async def handle_call_tool(name: str, arguments: Dict[str, Any]) -> List[types.TextContent]:
    """Handle tool calls"""
    try:
        if name == "query_ollama":
            result = await mcp_server.query_ollama(
                arguments.get("prompt", ""),
                arguments.get("model", "llama3.2"),
                arguments.get("context", "")
            )
        elif name == "web_search":
            result = await mcp_server.web_search(
                arguments.get("query", ""),
                arguments.get("num_results", 5),
                arguments.get("search_engine", "auto")
            )
        elif name == "scrape_website":
            result = await mcp_server.scrape_website(
                arguments.get("url", ""),
                arguments.get("extract_type", "text")
            )
        elif name == "research_with_web":
            result = await mcp_server.research_with_web(
                arguments.get("topic", ""),
                arguments.get("model", "llama3.2")
            )
        elif name == "check_ollama_updates":
            result = await mcp_server.check_ollama_updates()
        elif name == "update_ollama":
            result = await mcp_server.update_ollama()
        elif name == "pull_model":
            result = await mcp_server.pull_model(
                arguments.get("model_name", "")
            )
        else:
            result = f"Unknown tool: {name}"
        
        return [types.TextContent(type="text", text=result)]
        
    except Exception as e:
        logger.error(f"Error in tool call {name}: {e}")
        return [types.TextContent(type="text", text=f"Error: {str(e)}")]

async def main():
    """Run the enhanced MCP server"""
    from mcp.server.stdio import stdio_server
    
    async with stdio_server() as (read_stream, write_stream):
        await mcp_server.server.run(
            read_stream,
            write_stream,
            InitializationOptions(
                server_name="enhanced-ollama-mcp",
                server_version="2.0.0",
                capabilities=mcp_server.server.get_capabilities(
                    notification_options=NotificationOptions(),
                    experimental_capabilities={}
                )
            )
        )

if __name__ == "__main__":
    asyncio.run(main())
```


## Step 3: Configure Claude Code MCP Integration

Add the enhanced Ollama MCP server to Claude Code's configuration:

```bash
# Add the enhanced Ollama MCP server to Claude Code
claude mcp add ollama-web-assistant "python" "enhanced_ollama_mcp_server.py"

# Verify the server is registered
claude mcp list
```


## Step 4: Create Enhanced Slash Commands

Create custom slash commands that automatically trigger Ollama assistance with web connectivity:

```bash
# Create commands directory
mkdir -p ~/.claude/commands

# Web research command
cat > ~/.claude/commands/web-research.md << 'EOF'
You need to conduct comprehensive web research on: $ARGUMENTS

Use the research_with_web tool from Ollama to:
1. Generate targeted search queries
2. Perform web searches across multiple engines
3. Analyze and synthesize the results
4. Provide comprehensive insights with sources

This will give you up-to-date information from the internet.
EOF

# Web search command
cat > ~/.claude/commands/web-search.md << 'EOF'
Search the web for: $ARGUMENTS

Use the web_search tool from Ollama to:
1. Search across multiple search engines (SearXNG, DuckDuckGo)
2. Return relevant results with URLs and summaries
3. Provide current information from the internet

Query: $ARGUMENTS
EOF

# Website scraping command
cat > ~/.claude/commands/scrape-site.md << 'EOF'
Scrape content from website: $ARGUMENTS

Use the scrape_website tool from Ollama to:
1. Extract text content from the specified URL
2. Parse and clean the content
3. Provide structured information from the webpage

URL: $ARGUMENTS
EOF

# System update command
cat > ~/.claude/commands/update-system.md << 'EOF'
Check and update Ollama system: $ARGUMENTS

Use the check_ollama_updates and update_ollama tools to:
1. Check for available updates
2. Update Ollama to the latest version
3. Pull new models if specified
4. Verify system status

Options: $ARGUMENTS
EOF

# Error analysis command
cat > ~/.claude/commands/analyze-error.md << 'EOF'
You encountered an error and need help analyzing it: $ARGUMENTS

Use the query_ollama tool to:
1. Understand what the error means
2. Identify the root cause
3. Get step-by-step solutions
4. Learn prevention strategies

If web research is needed, use the research_with_web tool for additional context.

Error details: $ARGUMENTS
EOF

# Architectural guidance command
cat > ~/.claude/commands/architect.md << 'EOF'
You need architectural guidance for: $ARGUMENTS

Use the query_ollama tool to analyze:
1. Current approach evaluation
2. Alternative solutions
3. Pros and cons of each approach
4. Best practices recommendations
5. Implementation considerations

If current information is needed, use the research_with_web tool.

Question: $ARGUMENTS
EOF
```


## Step 5: Set Up Automated Hooks for Transparent Integration

Create hooks that automatically trigger Ollama assistance with web connectivity in specific scenarios:

```bash
# Create hooks directory
mkdir -p ~/.claude/hooks

# Enhanced error detection hook
cat > ~/.claude/hooks/error_detector.py << 'EOF'
#!/usr/bin/env python3
import json
import sys
import subprocess
import re

def detect_error_in_output(output):
    """Detect common error patterns in tool output"""
    error_patterns = [
        r'Error:\s*(.+)',
        r'Exception:\s*(.+)',
        r'Failed:\s*(.+)',
        r'fatal:\s*(.+)',
        r'TypeError:\s*(.+)',
        r'ValueError:\s*(.+)',
        r'SyntaxError:\s*(.+)',
        r'ImportError:\s*(.+)',
        r'ModuleNotFoundError:\s*(.+)',
        r'AttributeError:\s*(.+)',
        r'KeyError:\s*(.+)',
        r'IndexError:\s*(.+)',
        r'FileNotFoundError:\s*(.+)',
        r'PermissionError:\s*(.+)'
    ]
    
    for pattern in error_patterns:
        match = re.search(pattern, output, re.IGNORECASE | re.MULTILINE)
        if match:
            return match.group(1).strip()
    return None

def main():
    try:
        payload = json.loads(sys.stdin.read())
        
        # Check if this is a PostToolUse event with an error
        if payload.get('event_type') == 'PostToolUse':
            tool_output = payload.get('tool_output', '')
            
            # Detect errors in the output
            error = detect_error_in_output(tool_output)
            if error:
                # Return error code 2 to provide feedback to Claude
                feedback = f"""
Error detected in tool output: {error}

I recommend using the /analyze-error command to get help from Ollama with web research:
/analyze-error {error}

This will provide detailed analysis and solutions, including web research if needed.
"""
                print(feedback, file=sys.stderr)
                sys.exit(2)
        
        sys.exit(0)
        
    except Exception as e:
        print(f"Hook error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
EOF

chmod +x ~/.claude/hooks/error_detector.py

# Web connectivity monitor hook
cat > ~/.claude/hooks/web_monitor.py << 'EOF'
#!/usr/bin/env python3
import json
import sys
import asyncio
import aiohttp
from datetime import datetime
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class WebMonitor:
    def __init__(self):
        self.ollama_url = "http://localhost:11434"
        self.searxng_url = "http://localhost:8080"
    
    async def check_web_connectivity(self):
        """Check if web services are accessible"""
        services = {
            "Ollama": self.ollama_url,
            "SearXNG": self.searxng_url,
            "Internet": "https://www.google.com"
        }
        
        results = {}
        
        async with aiohttp.ClientSession() as session:
            for service, url in services.items():
                try:
                    async with session.get(url, timeout=5) as response:
                        results[service] = response.status == 200
                except Exception as e:
                    results[service] = False
                    logger.error(f"Error checking {service}: {e}")
        
        return results
    
    async def check_updates(self):
        """Check for system updates"""
        try:
            async with aiohttp.ClientSession() as session:
                # Check Ollama updates
                async with session.get(f"{self.ollama_url}/api/version") as response:
                    if response.status == 200:
                        data = await response.json()
                        current_version = data.get("version", "unknown")
                        
                        # Check GitHub for latest version
                        github_url = "https://api.github.com/repos/ollama/ollama/releases/latest"
                        async with session.get(github_url) as gh_response:
                            if gh_response.status == 200:
                                gh_data = await gh_response.json()
                                latest_version = gh_data.get("tag_name", "").replace("v", "")
                                
                                return {
                                    "current": current_version,
                                    "latest": latest_version,
                                    "update_available": current_version != latest_version
                                }
        except Exception as e:
            logger.error(f"Error checking updates: {e}")
        
        return {"error": "Could not check updates"}

async def main():
    monitor = WebMonitor()
    
    # Check web connectivity
    connectivity = await monitor.check_web_connectivity()
    
    # Check updates
    updates = await monitor.check_updates()
    
    # Report status
    status_report = {
        "timestamp": datetime.now().isoformat(),
        "connectivity": connectivity,
        "updates": updates
    }
    
    print(json.dumps(status_report, indent=2))

if __name__ == "__main__":
    asyncio.run(main())
EOF

chmod +x ~/.claude/hooks/web_monitor.py
```


## Step 6: Create Automated Notification System

Set up notifications to alert you when Ollama research or analysis is complete:

```python
# ~/.claude/hooks/notification_handler.py
#!/usr/bin/env python3
import json
import sys
import subprocess
import platform

def send_notification(message, title="Claude Code + Ollama"):
    """Send system notification"""
    system = platform.system()
    
    try:
        if system == "Darwin":  # macOS
            subprocess.run([
                'osascript', '-e',
                f'display notification "{message}" with title "{title}"'
            ])
        elif system == "Linux":
            subprocess.run(['notify-send', title, message])
        elif system == "Windows":
            subprocess.run([
                'powershell', '-Command',
                f'Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show("{message}", "{title}")'
            ])
    except Exception as e:
        print(f"Notification error: {e}", file=sys.stderr)

def main():
    try:
        payload = json.loads(sys.stdin.read())
        
        # Check if this is a Stop event after Ollama interaction
        if payload.get('event_type') == 'Stop':
            # Check if Ollama tools were used
            message = payload.get('message', '')
            if any(keyword in message.lower() for keyword in ['ollama', 'research', 'web_search', 'scrape']):
                send_notification(
                    "Claude Code has completed the Ollama-assisted task with web research",
                    "Research Complete"
                )
        
        sys.exit(0)
        
    except Exception as e:
        print(f"Notification hook error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
```


## Step 7: Configure Hooks in Claude Code

Add the hooks configuration to your Claude Code settings:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "",
        "hooks": [
          {
            "type": "command",
            "command": "python ~/.claude/hooks/error_detector.py"
          }
        ]
      }
    ],
    "Stop": [
      {
        "matcher": "",
        "hooks": [
          {
            "type": "command",
            "command": "python ~/.claude/hooks/notification_handler.py"
          }
        ]
      }
    ],
    "Notification": [
      {
        "matcher": "",
        "hooks": [
          {
            "type": "command",
            "command": "python ~/.claude/hooks/web_monitor.py"
          }
        ]
      }
    ]
  }
}
```


## Step 8: Usage Examples

### Comprehensive Web Research

```bash
# Conduct detailed research with web connectivity
> /web-research latest developments in AI coding assistants 2024

# This will:
# 1. Generate multiple targeted search queries
# 2. Search across SearXNG and DuckDuckGo
# 3. Analyze results with Ollama
# 4. Provide comprehensive insights with sources
```


### Real-time Web Search

```bash
# Direct web search with multiple engines
> /web-search "Python async programming best practices 2024"

# This will:
# 1. Search SearXNG and DuckDuckGo
# 2. Return current results with URLs
# 3. Provide summaries of found content
```


### Website Content Extraction

```bash
# Extract content from specific websites
> /scrape-site https://docs.python.org/3/library/asyncio.html

# This will:
# 1. Scrape the webpage content
# 2. Extract and clean text
# 3. Provide structured information
```


### System Updates and Maintenance

```bash
# Check and update Ollama
> /update-system check

# This will:
# 1. Check current Ollama version
# 2. Compare with latest GitHub release
# 3. Optionally perform updates
# 4. Report system status
```


### Error Analysis with Web Research

```bash
# Analyze errors with web context
> /analyze-error "ModuleNotFoundError: No module named 'asyncio'"

# This will:
# 1. Analyze the error with Ollama
# 2. Perform web research for solutions
# 3. Provide step-by-step fixes
# 4. Suggest prevention strategies
```


## Step 9: Advanced Configuration

### Web Search Configuration

Create a configuration file for customizing web search behavior:

```json
{
  "web_search": {
    "enabled": true,
    "primary_engine": "searxng",
    "fallback_engines": ["duckduckgo"],
    "max_results": 10,
    "timeout": 30,
    "cache_results": true,
    "cache_duration": 3600
  },
  "searxng": {
    "url": "http://localhost:8080",
    "categories": ["general", "news", "science"],
    "safe_search": 1,
    "format": "json"
  },
  "scraping": {
    "user_agent": "Mozilla/5.0 (compatible; Claude-Code-Bot/1.0)",
    "max_content_length": 50000,
    "timeout": 30,
    "extract_links": true,
    "extract_images": true
  },
  "updates": {
    "auto_check": true,
    "check_interval": 86400,
    "auto_update": false,
    "notify_updates": true
  }
}
```


### Automated Monitoring Script

Create a monitoring script that runs periodically:

```python
#!/usr/bin/env python3
import schedule
import time
import subprocess
import json
from datetime import datetime

def check_web_services():
    """Periodically check web services status"""
    result = subprocess.run(
        ["python", "~/.claude/hooks/web_monitor.py"],
        capture_output=True,
        text=True
    )
    
    if result.returncode == 0:
        status = json.loads(result.stdout)
        
        # Check for issues
        if not all(status["connectivity"].values()):
            send_alert("Web connectivity issues detected")
        
        if status["updates"].get("update_available"):
            send_alert("Ollama updates available")

def send_alert(message):
    """Send system alert"""
    subprocess.run(["notify-send", "Claude Code Alert", message])

# Schedule checks every 5 minutes
schedule.every(5).minutes.do(check_web_services)

while True:
    schedule.run_pending()
    time.sleep(60)
```


## Benefits of This Comprehensive Integration

1. **Complete Web Connectivity**: Ollama gains full internet access through SearXNG and DuckDuckGo search engines[^11][^12]
2. **Automated Updates**: System automatically checks for and can install Ollama updates[^13][^14]
3. **Seamless Operation**: All interactions between Claude Code and Ollama happen transparently[^1][^2]
4. **Enhanced Research**: Comprehensive web research capabilities with AI analysis[^3][^4]
5. **Error Recovery**: Automatic error detection and resolution assistance[^7][^8]
6. **Privacy-Focused**: Uses SearXNG for private web searches[^11][^12]
7. **Notification System**: Real-time alerts for completed tasks and system status[^15][^16]
8. **Extensible Architecture**: Easy to add new capabilities and customize behavior[^9][^10]

## Key Architectural Differences

This implementation addresses the critical distinction you identified:

- **Claude Code** retains its built-in web search capabilities for general queries[^1][^2]
- **Ollama** is enhanced with specialized web connectivity tools through the MCP server[^3][^4]
- **Integration layer** coordinates between Claude Code's native search and Ollama's enhanced capabilities[^5][^6]
- **Automated workflows** ensure transparent operation without manual intervention[^7][^8]


## Conclusion

This comprehensive integration creates a powerful development environment where Claude Code serves as your primary coding agent while leveraging Ollama's enhanced web connectivity for specialized research, error analysis, and system maintenance. The system operates transparently, automatically triggering appropriate assistance based on context, while maintaining the privacy and security benefits of local AI processing with full internet connectivity for current information gathering.

The integration provides both systems with their strengths: Claude Code's advanced reasoning and built-in tools, combined with Ollama's specialized capabilities enhanced by comprehensive web search, scraping, and system update management. This creates a unified, intelligent development environment that can handle complex coding tasks while staying current with the latest information and maintaining system health.

<div style="text-align: center">⁂</div>

[^1]: https://playbooks.com/mcp/claude-code

[^2]: https://www.anthropic.com/news/claude-code-remote-mcp

[^3]: https://github.com/GaryKu0/ollama-web-search

[^4]: https://www.youtube.com/watch?v=GMlSFIp1na0

[^5]: https://milvus.io/ai-quick-reference/what-are-tools-in-model-context-protocol-mcp-and-how-do-models-use-them

[^6]: https://www.anthropic.com/news/model-context-protocol

[^7]: https://apidog.com/blog/claude-code-hooks/

[^8]: https://docs.anthropic.com/en/docs/claude-code/hooks

[^9]: https://aiengineering.academy/Agents/MCP/CreateMCPServe/

[^10]: https://github.com/modelcontextprotocol/python-sdk

[^11]: https://www.tanyongsheng.com/note/setting-up-searxng-on-windows-localhost-your-private-customizable-search-engine/

[^12]: https://msty.ai/blog/setup-searxng-search

[^13]: https://www.linkedin.com/posts/technovangelist_lets-update-ollama-everywhere-activity-7262312518415458304-gOBf

[^14]: https://www.youtube.com/watch?v=DgMzvCFN0zQ

[^15]: https://www.linkedin.com/pulse/best-practices-designing-effective-automated-notification-workflows-cgwzc

[^16]: https://www.securitymagazine.com/articles/101595-the-important-role-of-automated-notification-systems-in-an-effective-security-solution

[^17]: https://docs.anthropic.com/en/docs/claude-code/mcp

[^18]: https://www.youtube.com/watch?v=9KKnNh89AGU

[^19]: https://www.philschmid.de/mcp-introduction

[^20]: https://www.reddit.com/r/ClaudeAI/comments/1lemtxx/claude_code_now_supports_remote_mcp_servers_no/

[^21]: https://www.reddit.com/r/ollama/comments/1ir1y64/web_search_for_ollama/

[^22]: https://github.com/modelcontextprotocol

[^23]: https://apidog.com/blog/how-to-quickly-build-a-mcp-server-for-claude-code/

[^24]: https://ollama.com/search

[^25]: https://modelcontextprotocol.io/specification/draft/server/tools

[^26]: https://www.youtube.com/watch?v=BJX6uJHIz5U

[^27]: https://www.youtube.com/watch?v=txflvGG_hIc

[^28]: https://google.github.io/adk-docs/tools/mcp-tools/

[^29]: https://scottspence.com/posts/configuring-mcp-tools-in-claude-code

[^30]: https://github.com/open-webui/open-webui

[^31]: https://brightdata.com/blog/ai/web-scraping-with-mcp

[^32]: https://github.com/mendableai/firecrawl-mcp-server

[^33]: https://www.youtube.com/watch?v=338CNrJgN08

[^34]: https://www.youtube.com/watch?v=vKvNmotlq88

[^35]: https://mcpmarket.com/categories/web-scraping-data-collection

[^36]: https://www.x-cmd.com/blog/250709/

[^37]: https://akashrajpurohit.com/blog/selfhost-searxng-for-privacy-focused-search/

[^38]: https://community.latenode.com/t/web-scraping-tool-with-ai-headless-browser-for-content-extraction/8727

[^39]: https://forums.developer.nvidia.com/t/help-needed-to-update-ollama-container-for-newer-model-support-jetpack-6-0-dp/307311

[^40]: https://discussion.scottibyte.com/t/searxng-privacy-respecting-self-hosted-search/461

[^41]: https://www.youtube.com/watch?v=6DXuadyaJ4g

[^42]: https://www.arsturn.com/blog/how-to-update-ollama-models-a-comprehensive-guide

[^43]: https://inteltechniques.com/blog/2025/07/11/extreme-privacy-update-self-hosted-searxng-guide/

[^44]: https://www.pulsemcp.com/servers/vishwajeetdabholkar-web-scraper

[^45]: https://github.com/ollama/ollama/issues/5099

[^46]: https://searxng.org

[^47]: https://matthewsanabria.dev/posts/running-jujutsu-with-claude-code-hooks/

[^48]: https://stackoverflow.com/questions/75017090/design-architecture-of-a-notification-system

[^49]: https://github.com/modelcontextprotocol/create-python-server

[^50]: https://github.com/disler/claude-code-hooks-mastery

[^51]: https://www.designgurus.io/course-play/grokking-system-design-interview-ii/doc/designing-a-notification-system

[^52]: https://www.digitalocean.com/community/tutorials/mcp-server-python

[^53]: https://docs.anthropic.com/en/docs/claude-code/settings

[^54]: https://www.sleuth.io/post/notifications-dont-let-silent-disasters-crush-your-dev-team/

[^55]: https://scrapfly.io/blog/posts/how-to-build-an-mcp-server-in-python-a-complete-guide

[^56]: https://www.builder.io/blog/claude-code

[^57]: https://www.amtelco.com/resources/improving-efficiency-automated-notifications-key-use-cases-and-best-practices/

[^58]: https://modelcontextprotocol.io/quickstart/server

[^59]: https://www.reddit.com/r/ClaudeAI/comments/1loodjn/claude_code_now_supports_hooks/

[^60]: https://www.youtube.com/watch?v=AIOH_9ZcVJk

