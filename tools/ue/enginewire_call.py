# Thin client for the EngineWire / MCP Automation Bridge native MCP HTTP endpoint
# (https://github.com/ChiR24/Unreal_mcp, plugin in <Project>/Plugins/McpAutomationBridge).
# Drop-in replacement for uemcp_call.py:
#   python enginewire_call.py                      -> list tools
#   python enginewire_call.py ue_python args.json  -> args.json = {"code": "<python run inside UE>"}
#   python enginewire_call.py <tool> args.json     -> raw native tool call (args passed as-is)
# Env: ENGINEWIRE_URL (default http://127.0.0.1:3000/mcp)
#      ENGINEWIRE_TOKEN or token file %USERPROFILE%\.enginewire_token (X-MCP-Capability-Token)
import sys, json, asyncio, os, base64
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client

URL = os.environ.get("ENGINEWIRE_URL", "http://127.0.0.1:3000/mcp")
def token():
    t = os.environ.get("ENGINEWIRE_TOKEN")
    if t: return t.strip()
    p = os.path.join(os.path.expanduser("~"), ".enginewire_token")
    return open(p, encoding="utf-8").read().strip() if os.path.exists(p) else ""

async def main():
    tk = token()
    hdr = {"X-MCP-Capability-Token": tk} if tk else {}
    async with streamablehttp_client(URL, headers=hdr, timeout=60, sse_read_timeout=1800) as (r, w, _):
        async with ClientSession(r, w) as s:
            await s.initialize()
            if len(sys.argv) < 2:
                for t in (await s.list_tools()).tools: print(t.name)
                return
            args = json.loads(open(sys.argv[2], encoding="utf-8-sig").read()) if len(sys.argv) > 2 else {}
            tool = sys.argv[1]
            if tool == "ue_python":  # uemcp-compatible alias
                tool, args = "system_control", {"action": "execute_python", "code": args["code"]}
            res = await s.call_tool(tool, args)
            sc = getattr(res, "structuredContent", None)
            if isinstance(sc, dict) and "output" in sc:  # execute_python result
                out = sc.get("output") or ""
                print(out.replace("\r\n", "\n").replace("\r", ""))
                if sc.get("error"): print(sc["error"], file=sys.stderr)
            else:
                for c in res.content:
                    if c.type == "text": print(c.text)
                    elif c.type == "image":
                        out = os.path.join(os.path.expanduser("~"), "enginewire_shot.png")
                        open(out, "wb").write(base64.b64decode(c.data)); print("IMAGE saved", out)
            if getattr(res, "isError", False): sys.exit(1)
asyncio.run(main())
