# SpiceDB MCP-Server
This is a highly experimental Model Context Protocol Server to let your llm use [SpiceDB](https://github.com/authzed/spicedb) 
to answer access- and before the fact audit-related questions (as defined by NIST in [this PDF](https://nvlpubs.nist.gov/nistpubs/specialpublications/nist.sp.800-162.pdf), Section 3.1.2.3).
It spins up an example spicedb environment using docker compose, and then uses the MCP-C#-SDK and the SpiceDB-C#-SDK to call `LookupSubjects`, `LookupResources`, `ReadRelationships` and `CheckBulkPermissions` under the hood.


## What it does
Here you can see an example of the output using Claude Desktop (this was done while developing, might not resemble the latest eecution paths. then again, determinism is not guaranteed altogether with AI, so who cares ;-):
### Table view:
![tableview.png](assets/tableview.png)
### Graph view:
![graphview.png](assets/graphview.png)

## Running the Code
- Install .net 9
- `cd SpiceDB-MCP` and there, use `dotnet run` to start the code once. then break using e.g. ctrl+c.
- add the appropriate config for Claude dekstop to integrate the server.
### Claude config
You need to integrate the MCP server into Claude. On MacOS, you need a file `claude_config.json` located in `~/Library/Application\ Support/Claude/`. 
If the file does not exist, create it. Then post / add the following json - change the Path to where the solution is located, for me it's for example `/Users/dguhr/git/dguhr/SpiceDB-MCP/SpiceDB-MCP`. 
```json
{
    "mcpServers": {
        "spicedb": {
            "command": "dotnet",
            "args": [
                "run",
                "--project",
                "/PATH/TO//SOLUTION",
                "--no-build"
            ],
            "env": {
                "SPICEDB_PSK": "testkey"
            }
        }
    }
}
```
> [!CAUTION]
> As of now this project uses the `MCP C#-SDK v0.1.0-preview.2` NuGet-Package. This package has a bug (or, maybe, the claude desktop client), so the spawned dotnet-processes DO NOT GET TERMINATED correctly. So after using, you want to run `ps aux | grep dotnet` or similar and kill the spawned dotnet processes.

### Starting up a local SpiceDB environment with Model and Schema
The docker-compose.yaml loads the schema and test relations from the `bootstrap` folder, so all you need to do is
```zsh
docker compose --env-file .env up -d
```

### Startup
- start claude desktop as mcp client (for now). Ask e.g. "who has access to document pay1". It may confuse the right permissions currently (read vs view) - this can be optimized for sure - it's a start. Currently just implements LookupSubjects ("who has access to..."-questions) and GetSchema for the LLM to know the right permissions, as a starting point. Currently only tested with claude sonnet 3.7

### ZED CLI-usage
If you wanna use zed, install zed locally - see [docs](https://authzed.com/docs/spicedb/getting-started/installing-zed).

then set the context:
`zed context set dev :50051 "testkey" --insecure`

then use it: 
`zed context use dev`

and make some calls, e.g. get the schema:
`zed schema read`

or get all members of a team:
`zed permission lookup-subjects team:smallprojectteam member user`

or get everyone who has access to a document:
`zed permission lookup-subjects document:pay1 view user`

or check which documents a user has access to:
`zed permission lookup-resources document view user:CTO`
