const { spawn } = require('child_process');

const child = process.platform === 'win32'
  ? spawn('C:\\WINDOWS\\System32\\cmd.exe', ['/d', '/s', '/c', 'npx -y @modelcontextprotocol/server-sequential-thinking'], {
      stdio: ['pipe', 'pipe', 'pipe']
    })
  : spawn('npx', ['-y', '@modelcontextprotocol/server-sequential-thinking'], {
      stdio: ['pipe', 'pipe', 'pipe']
    });

child.on('error', (err) => {
  console.error('Failed to launch MCP server process:', err.message);
  process.exitCode = 1;
});

let buffer = '';
let initialized = false;
let callSent = false;

function send(message) {
  const json = JSON.stringify(message);
  const header = `Content-Length: ${Buffer.byteLength(json, 'utf8')}\r\n\r\n`;
  child.stdin.write(header + json);
}

function tryParse() {
  while (true) {
    const sep = buffer.indexOf('\r\n\r\n');
    if (sep === -1) return;

    const header = buffer.slice(0, sep);
    const match = header.match(/Content-Length:\s*(\d+)/i);
    if (!match) throw new Error('Missing Content-Length header');
    const length = Number(match[1]);

    const start = sep + 4;
    if (buffer.length < start + length) return;

    const body = buffer.slice(start, start + length);
    buffer = buffer.slice(start + length);

    const message = JSON.parse(body);
    handleMessage(message);
  }
}

function handleMessage(message) {
  if (message.id === 1 && message.result && !initialized) {
    initialized = true;
    send({ jsonrpc: '2.0', method: 'notifications/initialized', params: {} });
    send({
      jsonrpc: '2.0',
      id: 2,
      method: 'tools/call',
      params: {
        name: 'sequential_thinking',
        arguments: {
          thought: 'Break down implementing an MCP installation task into clear steps.',
          thoughtNumber: 1,
          totalThoughts: 3,
          nextThoughtNeeded: true
        }
      }
    });
    callSent = true;
    return;
  }

  if (callSent && message.id === 2) {
    const output = message?.result?.content?.[0]?.text ?? JSON.stringify(message, null, 2);
    console.log('sequential_thinking tool response:\n');
    console.log(output);
    child.kill();
  }
}

child.stdout.on('data', (chunk) => {
  buffer += chunk.toString('utf8');
  try {
    tryParse();
  } catch (err) {
    console.error('Parse error:', err.message);
    child.kill();
    process.exitCode = 1;
  }
});

child.stderr.on('data', (chunk) => {
  // Keep stderr visible for troubleshooting while still allowing a successful run
  process.stderr.write(chunk.toString('utf8'));
});

child.on('exit', (code) => {
  if (!callSent) process.exitCode = code ?? 1;
});

send({
  jsonrpc: '2.0',
  id: 1,
  method: 'initialize',
  params: {
    protocolVersion: '2024-11-05',
    capabilities: {},
    clientInfo: {
      name: 'cline-install-check',
      version: '1.0.0'
    }
  }
});
