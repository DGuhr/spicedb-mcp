This is the starting point for a highly experimental Model Context Protocol Server for spicedb.

## Run the Code
- install .net 9
- `cd SpiceDB_MCP` and there, use `dotnet run` to start the code once. then break.
- start claude desktop as mcp client (for now). Ask e.g. "who has access to document pay1". It may confuse the right permissions currently (read vs view) - this can be optimized for sure - it's a start. Currently just implements LookupSubjects ("who has access to..."-questions) and GetSchema for the LLM to know the right permissions, as a starting point. Currently only tested with claude sonnet 3.7

## Starting up SpiceDB
### docker 
docker compose --env-file .env up -d

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
