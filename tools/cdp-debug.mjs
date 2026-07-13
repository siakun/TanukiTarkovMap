#!/usr/bin/env node
/**
cdp-debug.mjs - CefSharp CDP(Chrome DevTools Protocol) 디버깅 CLI

Purpose: Debug 빌드 앱이 여는 원격 디버깅 포트(기본 9222)에 접속해
CefSharp가 렌더링한 페이지의 DOM, JavaScript 실행 결과, 스크린샷을 앱 밖에서 조회한다.
주입 스크립트(Models/JavaScript/Scripts/*.js)의 적용 결과 검증에 쓴다.

Architecture: 의존성 없음. Node 22+ 내장 fetch/WebSocket만 사용한다.
/json/list로 페이지 타겟을 찾고, webSocketDebuggerUrl에 접속해 CDP 명령을 보낸다.

사용법:
  node tools/cdp-debug.mjs targets                디버깅 가능한 타겟 목록
  node tools/cdp-debug.mjs eval "<JS 표현식>"     페이지 컨텍스트에서 JS 실행 (Promise는 await)
  node tools/cdp-debug.mjs html [CSS선택자]       선택자의 outerHTML 출력 (생략 시 문서 전체)
  node tools/cdp-debug.mjs screenshot [저장경로]  렌더링 화면 PNG 캡처 (기본: OS 임시폴더, 경로 출력)

환경변수:
  CDP_PORT  원격 디버깅 포트 (기본 9222)

Critical Warnings:
- 앱이 Debug 빌드로 실행 중이어야 한다. Release 빌드는 포트를 열지 않는다.
- 타겟 선택은 devtools:// 를 제외한 첫 http(s) 페이지를 자동 선택한다.
  DevTools 창(F12)이 열려 있어도 영향 없다.
*/

import { writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const debugPort = process.env.CDP_PORT ?? "9222";
const baseUrl = `http://127.0.0.1:${debugPort}`;

async function fetchTargets() {
    let response;
    try {
        response = await fetch(`${baseUrl}/json/list`);
    } catch {
        console.error(
            `CDP 포트(${debugPort})에 연결할 수 없다. ` +
            `Debug 빌드 앱이 실행 중인지 확인한다. (Release 빌드는 포트를 열지 않는다)`
        );
        process.exit(1);
    }
    return response.json();
}

function pickPageTarget(targets) {
    const pageTargets = targets.filter(
        target => target.type === "page" && !target.url.startsWith("devtools://")
    );
    // about:blank보다 실제 사이트 페이지를 우선한다
    const sitePage = pageTargets.find(target => /^https?:/.test(target.url));
    const pageTarget = sitePage ?? pageTargets[0];
    if (!pageTarget) {
        console.error("디버깅 가능한 페이지 타겟이 없다.");
        process.exit(1);
    }
    return pageTarget;
}

async function connectToPage() {
    const pageTarget = pickPageTarget(await fetchTargets());
    const socket = new WebSocket(pageTarget.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => {
        socket.addEventListener("open", resolve, { once: true });
        socket.addEventListener("error", () => reject(new Error("WebSocket 연결 실패")), { once: true });
    });

    let nextCommandId = 1;
    const pendingCommands = new Map();
    socket.addEventListener("message", event => {
        const message = JSON.parse(event.data);
        if (message.id && pendingCommands.has(message.id)) {
            const { resolve, reject } = pendingCommands.get(message.id);
            pendingCommands.delete(message.id);
            if (message.error) {
                reject(new Error(`CDP 오류: ${message.error.message}`));
            } else {
                resolve(message.result);
            }
        }
    });

    const call = (method, params = {}) =>
        new Promise((resolve, reject) => {
            const commandId = nextCommandId++;
            pendingCommands.set(commandId, { resolve, reject });
            socket.send(JSON.stringify({ id: commandId, method, params }));
        });

    return { pageTarget, socket, call };
}

async function runTargets() {
    const targets = await fetchTargets();
    for (const target of targets) {
        console.log(`[${target.type}] ${target.title}\n  ${target.url}`);
    }
}

async function runEval(expression) {
    const { socket, call } = await connectToPage();
    const result = await call("Runtime.evaluate", {
        expression,
        returnByValue: true,
        awaitPromise: true,
    });
    socket.close();

    if (result.exceptionDetails) {
        console.error(
            "JS 예외:",
            result.exceptionDetails.exception?.description ?? result.exceptionDetails.text
        );
        process.exit(1);
    }

    const resultValue = result.result.value;
    if (resultValue === undefined) {
        // 직렬화 불가능한 값(DOM 노드 등)은 타입 설명만 출력한다
        console.log(result.result.description ?? `(${result.result.type})`);
    } else if (typeof resultValue === "string") {
        console.log(resultValue);
    } else {
        console.log(JSON.stringify(resultValue, null, 2));
    }
}

async function runHtml(selector) {
    const expression = selector
        ? `document.querySelector(${JSON.stringify(selector)})?.outerHTML ?? "(선택자와 일치하는 요소 없음)"`
        : "document.documentElement.outerHTML";
    await runEval(expression);
}

async function runScreenshot(savePath) {
    const { socket, call } = await connectToPage();
    const { data } = await call("Page.captureScreenshot", { format: "png" });
    socket.close();

    const outputPath = savePath ?? join(tmpdir(), `cdp-screenshot-${Date.now()}.png`);
    writeFileSync(outputPath, Buffer.from(data, "base64"));
    console.log(outputPath);
}

const [command, commandArg] = process.argv.slice(2);
switch (command) {
    case "targets":
        await runTargets();
        break;
    case "eval":
        if (!commandArg) {
            console.error('사용법: node tools/cdp-debug.mjs eval "<JS 표현식>"');
            process.exit(1);
        }
        await runEval(commandArg);
        break;
    case "html":
        await runHtml(commandArg);
        break;
    case "screenshot":
        await runScreenshot(commandArg);
        break;
    default:
        console.error(
            "사용법: node tools/cdp-debug.mjs <targets|eval|html|screenshot> [인자]\n" +
            "  targets                디버깅 가능한 타겟 목록\n" +
            '  eval "<JS 표현식>"     페이지 컨텍스트에서 JS 실행\n' +
            "  html [CSS선택자]       선택자의 outerHTML 출력 (생략 시 문서 전체)\n" +
            "  screenshot [저장경로]  렌더링 화면 PNG 캡처"
        );
        process.exit(1);
}
