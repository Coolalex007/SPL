# AGENTS.md
# Configuration File for ChatGPT Agent

## Agent Purpose
The agent supports development within this Unity project by analyzing, generating, modifying, and organizing code, documentation, assets, and configuration files.  
The agent should follow user instructions precisely, while being allowed to propose creative or optimized solutions when appropriate.

---

## Allowed Actions
The agent MAY perform the following actions:

- Read, analyze, and understand all project files and folders
- Modify existing files when instructed
- Create new scripts, assets, or documentation files when instructed
- Suggest improvements, refactorings, or optimizations
- Interpret Unity-specific contexts (C#, Scenes, Prefabs, Assets)
- Use internal tools (terminal, file operations, code execution sandbox)
- Propose alternative solutions creatively if useful  
  (but only *propose*—not autonomously execute)

---

## Forbidden Actions
The agent MUST NOT perform the following actions:

- **Do NOT run Git commands**  
  (no commits, pushes, pulls, branch changes, merges, or repository rewrites)

- Do NOT delete files unless explicitly instructed  
- Do NOT modify files without explicit permission  
- Do NOT execute Unity builds or play mode simulations  
- Do NOT access directories outside the project root  
- Do NOT act autonomously beyond the allowed creative suggestions

---

## Project Structure
This is a Unity project with the following relevant layout:

```
ProjectRoot/
│
├── Assets/
│   ├── Scripts/
│   ├── Scenes/
│   ├── Prefabs/
│   ├── Materials/
│   ├── Animations/
│   └── Other Unity resources...
│
├── Packages/
├── ProjectSettings/
└── AGENTS.md  (this file)
```

The agent should treat `Assets` as the primary programming location.

---

## Behavior Rules
- **User instructions override all defaults.**
- The agent should remain helpful, precise, and ideally creative when generating or improving code.
- Creativity is allowed **only** in the form of suggestions or enhanced implementations.
- The agent must clearly state assumptions when resolving missing or unclear information.
- The agent must avoid long autonomous operations unless explicitly approved.

---

## Safety & Transparency
- The agent must announce when performing significant operations (file creation, rewriting, transformations).
- The agent should warn the user before performing potentially destructive actions.
- No network calls or external installs unless explicitly instructed.

---

## End of Configuration
This configuration fully defines the allowed scope of autonomous behavior for the agent.
