# Thin stdio client for the UEMCP server (pip install uemcp) on the owner's PC.
# Usage: python uemcp_call.py                -> list tools
#        python uemcp_call.py ue_python args.json  (args.json = {"code": "<python run inside UE>"})
import sys, json, asyncio, shutil, os
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client
EXE = shutil.which("uemcp") or os.path.expandvars(r"%APPDATA%\Python\Python310\Scripts\uemcp.exe")
async def main():
    p = StdioServerParameters(command=EXE, args=[])
    async with stdio_client(p) as (r, w):
        async with ClientSession(r, w) as s:
            await s.initialize()
            if len(sys.argv) < 2:
                for t in (await s.list_tools()).tools: print(t.name)
                return
            args = json.loads(open(sys.argv[2], encoding="utf-8").read()) if len(sys.argv) > 2 else {}
            res = await s.call_tool(sys.argv[1], args)
            for c in res.content:
                if c.type == "text": print(c.text)
                elif c.type == "image":
                    import base64
                    out = os.path.join(os.path.expanduser("~"), "uemcp_shot.png")
                    open(out, "wb").write(base64.b64decode(c.data)); print("IMAGE saved", out)
asyncio.run(main())
