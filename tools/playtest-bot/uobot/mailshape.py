"""The shape every email to Chase has (cc-P157, the Golem's GolemMail.ps1 in Python): what happened in words first,
then "What to do:" with the one action or "Nothing to do", then the technical part under "Details".

Above Details: no file names, ISO dates, 24-hour times, epoch numbers or em dashes (plain_problems says which).
ASCII only, like the rest of the mail path.
"""
import re

RULE = "-------"


def shape(lead, do, sections=(), details=""):
    """The body: the lead, each (title, lines) section as bullets, What to do, then Details."""
    out = [lead.strip()]
    for title, lines in sections:
        lines = [l for l in lines if l]
        if lines:
            out += ["", title + ":"] + ["- " + l for l in lines]
    out += ["", "What to do: " + (do or "Nothing to do.")]
    if details.strip():
        out += ["", "Details", RULE, details.strip()]
    return "\n".join(out)


def top(subject, body):
    """The subject and the part of the body above Details."""
    i = body.find("\nDetails\n" + RULE)
    return subject + "\n" + (body if i < 0 else body[:i])


def plain_problems(subject, body):
    """What breaks the plain-words rules above Details: a list of reasons, empty when it reads plainly."""
    t = top(subject, body)
    why = []
    m = re.search(r"\b[\w.-]+\.(json|jsonl|ps1|md|log|py|cmd|txt)\b", t)
    if m:
        why.append("a file name: " + m.group(0))
    m = re.search(r"\b\d{4}-\d{2}-\d{2}", t)
    if m:
        why.append("an ISO date: " + m.group(0))
    m = re.search(r"\b1\d{9}\b", t)
    if m:
        why.append("an epoch number: " + m.group(0))
    m = re.search(r"\b([01]?\d|2[0-3]):[0-5]\d\b(?!\s?[AP]M)(?!:\d)", t)
    if m:
        why.append("a 24-hour time: " + m.group(0))
    if "—" in t:
        why.append("an em dash")
    m = re.search(r"\b[0-9a-f]{7,40}\b", t)
    if m and re.search(r"\d", m.group(0)) and re.search(r"[a-f]", m.group(0)):
        why.append("a commit hash: " + m.group(0))
    m = re.search(r"\b(FAILED|STOPPED|NEEDS YOU)\b", t)
    if m:
        why.append("a state word in capitals: " + m.group(0))
    return why
