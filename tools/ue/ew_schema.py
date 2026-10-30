# ew_schema.py (R58): print EngineWire tool descriptions / input schemas (never prints the token).
# Usage: python ew_schema.py <tool> [<tool>...] [--grep word]
import sys, json, asyncio, os, re
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client
URL = os.environ.get("ENGINEWIRE_URL", "http://127.0.0.1:3000/mcp")
def token():
    t = os.environ.get("ENGINEWIRE_TOKEN")
    if t: return t.strip()
    p = os.path.join(os.path.expanduser("~"), ".enginewire_token")
    return open(p, encoding="utf-8").read().strip() if os.path.exists(p) else ""
async def main(names, grep):
    tk = token(); hdr = {"X-MCP-Capability-Token": tk} if tk else {}
    async with streamablehttp_client(URL, headers=hdr) as (r, w, _):
        async with ClientSession(r, w) as s:
            await s.initialize()
            for t in (await s.list_tools()).tools:
                if t.name not in names: continue
                j = json.dumps(t.inputSchema)
                if grep:
                    props = t.inputSchema.get("properties", {})
                    print(t.name, "props:", list(props)[:80])
                    print(" desc:", [l for l in (t.description or "").splitlines() if grep in l.lower()][:20])
                    for k, v in props.items():
                        if grep in json.dumps(v).lower() or grep in k.lower(): print("  ", k, json.dumps(v)[:300])
                else:
                    print(t.name, t.description); print(j)
if __name__ == "__main__":
    a = sys.argv[1:]; g = None
    if "--grep" in a: i = a.index("--grep"); g = a[i + 1].lower(); a = a[:i] + a[i + 2:]
    asyncio.run(main(a, g))
