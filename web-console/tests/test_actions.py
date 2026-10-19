"""cc-P182: the web console's Golem buttons. Run from the repo root:

    python -m unittest discover -s web-console/tests

Who may act (only Chase's login, only through the uoconsole sidecar, proved by the secret only the sidecar holds), the
typed word checked on the server, one action at a time, each button reaching the console's runner with its inputs (a
fake runner records each call), the output streamed back, the action log (never a header or a token), and the
sidecar's proof forwarder."""
import http.server
import json
import os
import shutil
import socket
import subprocess
import sys
import tempfile
import threading
import time
import unittest
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
WEB = os.path.dirname(HERE)
REPO = os.path.dirname(WEB)
FIX = os.path.join(HERE, "fixtures")
sys.path.insert(0, WEB)
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(WEB, "tailscale"))
import panels  # noqa: E402
import server  # noqa: E402

PROOF = "p182-test-proof-0123456789abcdef0123456789abcdef"
CHASE = {"Tailscale-User-Login": "chase@example.com", "X-UOConsole-Proof": PROOF}
INPUTS = {"model": "Opus", "cap": "3", "until": "2026-10-20 06:00", "start": "22:30", "end": "06:30", "prompt": "P151"}
FLAGS = {"model": "-Model", "cap": "-Cap", "until": "-Until", "start": "-NightStart", "end": "-NightEnd", "prompt": "-Prompt"}


def free_port():
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    p = s.getsockname()[1]
    s.close()
    return p


def actions():
    with open(os.path.join(WEB, "golem-actions.json"), encoding="utf-8") as f:
        return json.load(f)["actions"]


def word_for(e):
    return {"#prompt": "P151", "#stopped": "P140"}.get(e["word"], e["word"])


class Proof(unittest.TestCase):
    """panels.decide on the tailnet listener: the sidecar's proof first, then the login."""
    ALLOWED = ["chase@example.com"]

    def test_the_tailnet_needs_the_sidecars_proof(self):
        h = {"Tailscale-User-Login": "chase@example.com"}
        self.assertEqual((False, "no sidecar proof"), panels.decide(panels.TAILNET, h, self.ALLOWED, PROOF))
        self.assertEqual((False, "no sidecar proof"), panels.decide(panels.TAILNET, dict(h, **{"X-UOConsole-Proof": "forged"}), self.ALLOWED, PROOF))
        self.assertTrue(panels.decide(panels.TAILNET, dict(h, **{"X-UOConsole-Proof": PROOF}), self.ALLOWED, PROOF)[0])

    def test_no_proof_configured_refuses_the_tailnet(self):
        self.assertEqual((False, "no sidecar proof configured"), panels.decide(panels.TAILNET, CHASE, self.ALLOWED, ""))

    def test_the_proof_does_not_let_another_login_in(self):
        h = {"Tailscale-User-Login": "someone@example.com", "X-UOConsole-Proof": PROOF}
        self.assertEqual((False, "login not allowed"), panels.decide(panels.TAILNET, h, self.ALLOWED, PROOF))
        self.assertEqual((False, "no Tailscale identity"), panels.decide(panels.TAILNET, {"X-UOConsole-Proof": PROOF}, self.ALLOWED, PROOF))

    def test_the_local_listener_is_unchanged(self):
        self.assertEqual((True, "local"), panels.decide(panels.LOCAL, {}, [], ""))


class LiveActions(unittest.TestCase):
    """The real handlers on two loopback ports, with the fake runner recording each call."""

    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.mkdtemp(prefix="p182-web-")
        with open(os.path.join(cls.tmp, "settings.json"), "w", encoding="utf-8") as f:
            json.dump({"allowedLogins": ["chase@example.com"]}, f)
        with open(os.path.join(cls.tmp, "proof.key"), "w", encoding="ascii") as f:
            f.write(PROOF + "\n")
        cls.status = os.path.join(cls.tmp, "console-status.json")
        shutil.copy(os.path.join(FIX, "console-status.json"), cls.status)
        cls.record = os.path.join(cls.tmp, "calls.jsonl")
        runner = [sys.executable, os.path.join(FIX, "fake_runner.py"), cls.record]
        cls.console = server.Console(REPO, os.path.join(cls.tmp, "notes"), cls.tmp, cls.status, collector=False, runner=runner)
        cls.local_port, cls.tail_port = free_port(), free_port()
        cls.servers = []
        for listener, port in ((panels.LOCAL, cls.local_port), (panels.TAILNET, cls.tail_port)):
            s = server.ThreadingHTTPServer(("127.0.0.1", port), server.make_handler(cls.console, listener, os.path.join(cls.tmp, "access.log")))
            threading.Thread(target=s.serve_forever, daemon=True).start()
            cls.servers.append(s)

    @classmethod
    def tearDownClass(cls):
        for s in cls.servers:
            s.shutdown()
            s.server_close()
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def setUp(self):
        self.wait_idle()
        if os.path.exists(self.record):
            os.remove(self.record)

    def calls(self):
        if not os.path.exists(self.record):
            return []
        with open(self.record, encoding="utf-8") as f:
            return [json.loads(l) for l in f if l.strip()]

    def req(self, port, path, body=None, headers=None, method=None, ctype="application/json"):
        data = None if body is None else (body if isinstance(body, bytes) else json.dumps(body).encode("utf-8"))
        h = dict(headers or {})
        if data is not None:
            h.setdefault("Content-Type", ctype)
        r = urllib.request.Request("http://127.0.0.1:%d%s" % (port, path), data=data, headers=h, method=method or ("POST" if data is not None else "GET"))
        try:
            with urllib.request.urlopen(r, timeout=10) as x:
                return x.status, x.read().decode("utf-8")
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode("utf-8")

    def act(self, key, typed, inputs=None, headers=None, port=None):
        h = dict(CHASE, **{"X-UOConsole-Action": "1"})
        h.update(headers or {})
        return self.req(port or self.tail_port, "/api/action", {"key": key, "typed": typed, "inputs": inputs or {}}, h)

    def job(self, since=0):
        code, body = self.req(self.tail_port, "/api/action?since=%d" % since, headers=CHASE)
        self.assertEqual(200, code, body)
        return json.loads(body)

    def wait_idle(self, timeout=15):
        end = time.time() + timeout
        while time.time() < end:
            j = self.console.job_view(0)
            if not j or not j.get("running"):
                return j
            time.sleep(0.05)
        self.fail("the action did not end")

    # --- who may act ------------------------------------------------------------------------------------------------
    def test_only_chase_through_the_sidecar_may_act(self):
        no_proof = {"Tailscale-User-Login": "chase@example.com", "X-UOConsole-Proof": ""}
        self.assertEqual(403, self.act("poke", "poke", headers=no_proof)[0], "no proof")
        self.assertEqual(403, self.act("poke", "poke", headers={"X-UOConsole-Proof": "forged"})[0], "a forged proof")
        self.assertEqual(403, self.act("poke", "poke", headers={"Tailscale-User-Login": "daughter@example.com"})[0], "another login")
        self.assertEqual(403, self.act("poke", "poke", headers={"Tailscale-User-Login": ""})[0], "no login")
        code, body = self.act("poke", "poke", port=self.local_port)
        self.assertEqual(403, code, "this PC's own page is read only")
        self.assertIn("uoconsole", body)
        self.assertEqual([], self.calls(), "nothing ran")

    def test_cross_site_requests_are_refused(self):
        self.assertEqual(403, self.req(self.tail_port, "/api/action", {"key": "poke", "typed": "poke"}, CHASE)[0], "no X-UOConsole-Action header")
        self.assertEqual(403, self.act("poke", "poke", headers={"Origin": "https://evil.example"})[0], "another origin")
        self.assertEqual(403, self.act("poke", "poke", headers={"Sec-Fetch-Site": "cross-site"})[0], "a cross-site fetch")
        h = dict(CHASE, **{"X-UOConsole-Action": "1"})
        self.assertEqual(415, self.req(self.tail_port, "/api/action", b"key=poke&typed=poke", h, ctype="application/x-www-form-urlencoded")[0], "a form post")
        self.assertEqual([], self.calls())

    # --- the word ---------------------------------------------------------------------------------------------------
    def test_the_word_is_checked_on_the_server(self):
        code, body = self.act("poke", "")
        self.assertEqual(400, code)
        self.assertIn("type poke", body)
        self.assertEqual(400, self.act("wrapup", "poke")[0], "another button's word")
        self.assertEqual(400, self.act("runnext", "P150", {"prompt": "P151"})[0], "a number not the picked one")
        self.assertEqual([], self.calls(), "nothing ran without its word")
        self.assertEqual(202, self.act("poke", " POKE ")[0], "case and spaces do not matter (phones)")
        self.wait_idle()
        c = self.calls()
        self.assertEqual(1, len(c))
        self.assertEqual("POKE", c[0][c[0].index("-Typed") + 1].strip(), "the runner checks the word again")

    def test_bad_inputs_are_refused(self):
        for k, v in (("model", "GPT"), ("cap", "-1"), ("prompt", "-Key"), ("start", "25:99x"), ("until", "now; rm")):
            e = [a for a in actions() if k in (a.get("inputs") or [])][0]
            self.assertEqual(400, self.act(e["key"], word_for(e), dict(INPUTS, **{k: v}))[0], k + "=" + v)
        self.assertEqual(400, self.act("poke", 'poke" -Key wake')[0], "a word with quotes")
        self.assertEqual(404, self.act("format-c", "x")[0], "no such button")
        self.assertEqual([], self.calls())

    # --- one at a time ----------------------------------------------------------------------------------------------
    def test_one_action_at_a_time(self):
        hold = self.record + ".sleep"
        open(hold, "w").close()
        try:
            self.assertEqual(202, self.act("poke", "poke")[0])
            code, body = self.act("rest", "rest")
            self.assertEqual(409, code)
            j = json.loads(body)
            self.assertEqual("Poke", j["running"]["label"], body)
        finally:
            os.remove(hold)
        self.wait_idle()
        self.assertEqual(202, self.act("rest", "rest")[0], "free again once it ended")
        self.wait_idle()
        self.assertEqual(["poke", "rest"], [c[c.index("-Key") + 1] for c in self.calls()])

    # --- each button calls the console's runner ---------------------------------------------------------------------
    def test_each_button_calls_the_consoles_runner_with_its_inputs(self):
        for e in actions():
            if e.get("disabled"):
                code, body = self.act(e["key"], e["word"])
                self.assertEqual(409, code, e["key"])
                self.assertIn("P149", body)
                continue
            code, body = self.act(e["key"], word_for(e), INPUTS)
            self.assertEqual(202, code, e["key"] + ": " + body)
            self.wait_idle()
        got = {c[c.index("-Key") + 1]: c for c in self.calls()}
        for e in actions():
            if e.get("disabled"):
                self.assertNotIn(e["key"], got, "a disabled button runs nothing")
                continue
            c = got[e["key"]]
            self.assertTrue(c[0].endswith("-NoProfile") or "-Key" in c, c)
            for name in e.get("inputs") or []:
                self.assertEqual(INPUTS[name], c[c.index(FLAGS[name]) + 1], e["key"] + " " + name)
            for name in set(FLAGS) - set(e.get("inputs") or []):
                self.assertNotIn(FLAGS[name], c, e["key"] + " passes only its own inputs")

    def test_the_real_runner_is_the_consoles_script(self):
        c = server.Console(REPO, self.tmp, self.tmp, self.status, collector=False)
        self.assertTrue(c.runner[-1].endswith(os.path.join("scripts", "Invoke-WebConsoleAction.ps1")), c.runner)
        self.assertIn("-NoProfile", c.runner)

    # --- the output and the log -------------------------------------------------------------------------------------
    def test_output_streams_back_and_the_result_is_the_consoles_line(self):
        self.assertEqual(202, self.act("poke", "poke")[0])
        self.wait_idle()
        j = self.job()
        self.assertFalse(j["running"])
        self.assertEqual("poke", j["key"])
        self.assertEqual("golem.poke: done.", j["result"])
        self.assertEqual(0, j["code"])
        texts = [l["text"] for l in j["lines"]]
        self.assertIn("Web console: poke", texts)
        self.assertEqual("green", j["lines"][-1]["color"])
        self.assertNotIn("abc123secretvalue", json.dumps(j), "lines are redacted on the way out")
        self.assertNotIn("abc123secretvalue", json.dumps(self.console.job_view(0)), "and redacted when kept")
        later = self.job(len(j["lines"]) - 1)
        self.assertEqual(1, len(later["lines"]), "since= returns only the new lines")

    def test_the_action_log_never_holds_a_header_or_token(self):
        h = {"Authorization": "Bearer abcdefghijklmnop", "Cookie": "session=zzz-cookie-value"}
        self.assertEqual(202, self.act("poke", "poke", headers=h)[0])
        self.wait_idle()
        with open(os.path.join(self.tmp, "actions.log"), encoding="utf-8") as f:
            text = f.read()
        last = json.loads(text.strip().splitlines()[-1])
        self.assertEqual("poke", last["key"])
        self.assertEqual("golem.poke", last["action"])
        self.assertEqual("chase@example.com", last["who"])
        self.assertEqual("golem.poke: done.", last["result"])
        self.assertTrue(last["time"][:4].isdigit())
        for secret in (PROOF, "abcdefghijklmnop", "zzz-cookie-value", "abc123secretvalue", "Bearer", "Tailscale-User-Login"):
            self.assertNotIn(secret, text)
        code, body = self.req(self.tail_port, "/api/status", headers=CHASE)
        tl = json.loads(body)["panels"]["timeline"]
        self.assertTrue(any(e["kind"] == "web" and "Poke" in e["text"] for e in tl), "the action is on the Timeline")
        self.assertNotIn(PROOF, body)

    def test_the_page_is_told_the_buttons_and_whether_it_may_act(self):
        j = json.loads(self.req(self.tail_port, "/api/status", headers=CHASE)[1])
        keys = [a["key"] for a in j["actions"]["buttons"]]
        self.assertIn("retry", keys)
        self.assertTrue(j["actions"]["canAct"])
        local = json.loads(self.req(self.local_port, "/api/status")[1])
        self.assertFalse(local["actions"]["canAct"])
        self.assertIn("uoconsole", local["actions"]["why"])


class ProofForwarder(unittest.TestCase):
    """web-console/tailscale/proof_proxy.py: in the sidecar it adds the proof to every request and passes the rest on."""

    def test_it_sets_the_proof_and_passes_the_request_and_the_answer(self):
        import proof_proxy
        seen = {}

        class Back(http.server.BaseHTTPRequestHandler):
            def log_message(self, *a):
                pass

            def do_POST(self):
                seen["proof"] = self.headers.get_all("X-UOConsole-Proof")
                seen["login"] = self.headers.get("Tailscale-User-Login")
                seen["host"] = self.headers.get("Host")
                seen["body"] = self.rfile.read(int(self.headers.get("Content-Length") or 0))
                out = b"x" * 70000
                self.send_response(201)
                self.send_header("Content-Length", str(len(out)))
                self.send_header("X-Back", "yes")
                self.end_headers()
                self.wfile.write(out)

        bp, pp = free_port(), free_port()
        back = http.server.ThreadingHTTPServer(("127.0.0.1", bp), Back)
        threading.Thread(target=back.serve_forever, daemon=True).start()
        prox = proof_proxy.make_server("127.0.0.1", pp, "127.0.0.1", bp, PROOF)
        threading.Thread(target=prox.serve_forever, daemon=True).start()
        try:
            r = urllib.request.Request("http://127.0.0.1:%d/api/action" % pp, data=b'{"key":"poke"}', method="POST",
                                       headers={"X-UOConsole-Proof": "forged", "Tailscale-User-Login": "chase@example.com",
                                                "Host": "uoconsole.example.ts.net", "Content-Type": "application/json"})
            with urllib.request.urlopen(r, timeout=10) as x:
                self.assertEqual(201, x.status)
                self.assertEqual("yes", x.headers.get("X-Back"))
                self.assertEqual(70000, len(x.read()))
            self.assertEqual([PROOF], seen["proof"], "a forged proof is replaced, never passed on")
            self.assertEqual("chase@example.com", seen["login"])
            self.assertEqual("uoconsole.example.ts.net", seen["host"])
            self.assertEqual(b'{"key":"poke"}', seen["body"])
        finally:
            for s in (prox, back):
                s.shutdown()
                s.server_close()

    def test_it_refuses_to_start_on_the_loopback_netstack_forwards_to(self):
        import proof_proxy
        self.assertTrue(proof_proxy.bind_refusal("127.0.0.1"), "where tailscaled hands a direct tailnet connection")
        self.assertTrue(proof_proxy.bind_refusal("0.0.0.0"))
        self.assertTrue(proof_proxy.bind_refusal("192.168.1.58"))
        self.assertEqual("", proof_proxy.bind_refusal("127.0.0.2"))
        with self.assertRaises(SystemExit):
            proof_proxy.main(["--bind", "127.0.0.1", "--proof-file", os.devnull])
        with self.assertRaises(SystemExit):
            proof_proxy.main(["--bind", "0.0.0.0", "--proof-file", os.devnull])

    def test_the_sidecar_config_serves_through_the_forwarder_only(self):
        with open(os.path.join(WEB, "tailscale", "serve.json"), encoding="utf-8") as f:
            serve = json.load(f)
        self.assertEqual({"Proxy": "http://127.0.0.2:8097"}, serve["Web"]["${TS_CERT_DOMAIN}:443"]["Handlers"]["/"])
        self.assertFalse(serve["AllowFunnel"]["${TS_CERT_DOMAIN}:443"])
        with open(os.path.join(WEB, "tailscale", "docker-compose.yml"), encoding="utf-8") as f:
            compose = f.read()
        self.assertIn("network_mode: service:tailscale", compose)
        self.assertNotIn("ports:", compose)
        self.assertIn("UOCONSOLE_PROOF", compose)
        self.assertIn(":/run/uoconsole/proof.key:ro", compose)


EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"


@unittest.skipUnless(os.path.exists(EDGE), "Microsoft Edge is not installed")
class PhoneControls(unittest.TestCase):
    """The Golem's buttons on a 390 px phone, through the tailnet listener (the proof and the login set as the sidecar
    would), with the fake runner: big buttons two a row, the sheet within the screen, Run lit only by the word, the
    output and the console's result line, and the art the repo has in its slots."""

    def test_wake_poke_and_read_the_result_one_handed(self):
        import cdp
        tmp = tempfile.mkdtemp(prefix="p182-phone-")
        try:
            with open(os.path.join(tmp, "settings.json"), "w", encoding="utf-8") as f:
                json.dump({"allowedLogins": ["chase@example.com"]}, f)
            with open(os.path.join(tmp, "proof.key"), "w", encoding="ascii") as f:
                f.write(PROOF)
            status = os.path.join(tmp, "console-status.json")
            shutil.copy(os.path.join(FIX, "console-status.json"), status)
            record = os.path.join(tmp, "calls.jsonl")
            console = server.Console(REPO, os.path.join(tmp, "notes"), tmp, status, collector=False,
                                     runner=[sys.executable, os.path.join(FIX, "fake_runner.py"), record])
            port = free_port()
            s = server.ThreadingHTTPServer(("127.0.0.1", port), server.make_handler(console, panels.TAILNET))
            threading.Thread(target=s.serve_forever, daemon=True).start()
            try:
                with cdp.Browser(EDGE) as b:
                    b.phone(390, 844)
                    b.call("Network.enable")
                    b.call("Network.setExtraHTTPHeaders", headers=CHASE)
                    b.open("http://127.0.0.1:%d/#golem" % port)
                    self.assertTrue(b.wait_for("document.querySelectorAll('#ctl-buttons button.ctl').length > 5"), "the buttons are drawn")
                    self.assertEqual("", b.eval("document.getElementById('ctl-note').textContent"), "this page may act")
                    keys = b.eval("Array.prototype.map.call(document.querySelectorAll('#ctl-buttons button.ctl'), function (x) { return x.getAttribute('data-key'); }).join(',')")
                    for k in ("wake", "poke", "rest", "stoppoint", "longrun", "runnext", "retry", "away-on"):
                        self.assertIn(k, keys.split(","), keys)
                    self.assertNotIn("wrapup-cancel", keys.split(","), "only the half of a pair that applies")
                    small = b.eval("Array.prototype.filter.call(document.querySelectorAll('#ctl-buttons button.ctl'), function (x) { return x.getBoundingClientRect().height < 44 || x.getBoundingClientRect().width < 140; }).length")
                    self.assertEqual(0, small, "every button is a big thumb target")
                    self.assertLessEqual(b.eval("document.documentElement.scrollWidth"), 391)
                    self.assertTrue(b.eval("!!document.querySelector('#golem-state img.figure')"), "the Golem's art from its slot")
                    self.assertTrue(b.eval("document.querySelectorAll('#tiles img.icon').length >= 5"), "the tile icons from their slots")
                    b.eval("document.querySelector('#ctl-buttons button[data-key=poke]').click()")
                    self.assertTrue(b.wait_for("!document.getElementById('sheet').hidden"), "the sheet opens")
                    box = b.eval("JSON.stringify(document.querySelector('.sheetbox').getBoundingClientRect())")
                    r = json.loads(box)
                    self.assertGreaterEqual(r["left"], 0)
                    self.assertLessEqual(r["right"], 390.5, box)
                    self.assertGreater(r["top"], 300, "the sheet sits in the lower half, under the thumb: " + box)
                    self.assertTrue(b.eval("document.getElementById('sheet-run').disabled"), "Run is dark until the word is typed")
                    self.assertEqual("poke", b.eval("document.getElementById('sheet-want').textContent"))
                    b.eval("var i = document.getElementById('sheet-word'); i.value = 'Poke'; i.dispatchEvent(new Event('input'));")
                    self.assertFalse(b.eval("document.getElementById('sheet-run').disabled"), "lit by the word (any case)")
                    b.eval("document.getElementById('sheet-run').click()")
                    self.assertTrue(b.wait_for("document.getElementById('job-result').textContent === 'golem.poke: done.'"), "the console's result line")
                    self.assertIn("Web console: poke", b.eval("document.getElementById('job-lines').textContent"))
                    self.assertNotIn("abc123secretvalue", b.eval("document.body.innerText"))
                    with open(record, encoding="utf-8") as f:
                        self.assertEqual(1, len(f.read().strip().splitlines()), "one call")
                    if os.environ.get("P182_SHOT"):
                        b.eval("document.querySelector('#ctl-buttons button[data-key=stoppoint]').click()")
                        b.wait_for("!document.getElementById('sheet').hidden")
                        b.screenshot(os.environ["P182_SHOT"])
            finally:
                s.shutdown()
                s.server_close()
        finally:
            shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
