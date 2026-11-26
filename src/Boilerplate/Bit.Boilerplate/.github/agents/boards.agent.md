---
name: boards
description: Builds a board, a page in the Workbench's Boards section (`/boards`) that shows the user something instead of a long answer in the chat - how a part of the project works, options weighed side by side, a plan or a report - visual first and in very few, everyday words, on the app's own bit BlazorUI components and theme, in light and dark, on every device and right to left, and hands it over as its address.
---

# Boards: show it, don't tell it

A board is a page an agent builds when an answer is easier to see than to read. It lives in the Workbench (the
**workbench** skill, `.github/agents/workbench.agent.md`) in a section of its own, Boards, next to Pages, Flows and
Progress: `/boards` lists them by kind, newest first, and each opens at `/boards/<id>` with the others listed at its side,
on the app's own components and theme, in light and dark, on a phone and right to left.

Two examples ship with the Workbench. Look at both before you build your first:
`src/Workbench/Components/Pages/Boards/Payments`, a dated comparison of payment providers and of the stores' terms with a
calculator, and `src/Workbench/Components/Pages/Boards/Deploy`, how this project gets from a change to its users, drawn
for the options it was created with.

## 1. When to build one

- The answer is a picture or a comparison: how parts connect (an architecture, a flow, a pipeline), options weighed
  against each other (providers, libraries, approaches, prices), a plan with its steps, a report of what was found or
  done, how a part of the project works.
- Or the user will come back to it, show it to someone, or decide from it.
- A short answer, a single fact or a code change stays in the chat. When it is a close call, answer in the chat and offer
  the board in one line.
- It isn't a design of a page of the app, which the **workbench** skill designs, nor a long task's progress, which goes on
  the Progress page (its section 9). Under **ai-dlc**, a phase that shows the user a plan, options or a report can show it
  as a board.
- Never put a secret, a key or a password on it, nor anything the user wouldn't show everyone who gets its link.

## 2. Plan it

- Settle one subject, who reads it and the one question it answers; a short name for that question is its title.
- Ground it in the subject: read the code, the configuration, `.github/workflows`, `.docs`, `specs/`. Read an outside
  fact, like a price, a fee or a rule, on its owner's own page, and date it. Real names and numbers only, never a figure
  you didn't read somewhere; an estimate says that it is one.
- Find the visual that carries each idea before any sentence: a path with stops for a flow, cards with the same rows for
  options, big numbers for what matters most, bars to one scale for amounts, a fork for a decision, a slider for a
  what-if. Start with `SearchBitBlazorUI` (`AGENTS.md` section 3) for each part; a diagram is drawn in the page as markup
  or inline SVG, in the theme's tokens.
- Budget the words. A title of a few words and one line under it; each block a short heading and at most one line; labels
  of one to three words; captions under eight. No paragraphs: a board that needs one is a document, and a document
  belongs in `.docs`.
- Write for someone who doesn't build software: everyday words for what happens ("Tested automatically", "Your server
  switches to the new version"), not the tools that do it. File names, commands and setting names go in small print or
  in a fold at the foot.

## 3. Build it

- One folder per board, `src/Workbench/Components/Pages/Boards/<Name>/`: `<Name>Board.razor` with its `.razor.cs` and
  `.razor.scss`, and its content in `<Name>Facts.cs` as C# records, so the markup stays short and the facts sit in one
  place.
- Frame it with the blocks in `src/Workbench/Components/Boards`: `BoardFrame` around it all (title, the line under it, an
  icon, its tone, when its facts were read, a short note, its language, an `Aside` beside the title and its sources),
  a `BoardBlock` per idea (an id a link can point at, a heading, at most one line), `BoardNumbers` for big numbers and a
  `BoardSource` for each page or file a fact came from, which `BoardFrame` folds at the foot.
- Add it to `WorkbenchBoardCatalog.Build` (`src/Workbench/Infrastructure/Services/WorkbenchBoardCatalog.cs`) with its id,
  title, a one-line summary, an icon, its component, the day it was made, its tone and its kind (`HowItWorks`,
  `Comparison`, `Plan` or `Report`), which the list groups the boards by. Hot reload brings it in. While
  you work, the title and the summary can be plain text where the `nameof(WorkbenchStrings.…)` keys go; the Workbench
  shows them as written.
- Write it in the user's language, as plain text in the board, and give `BoardFrame` that language: it then reads in it,
  right to left for one that is, whatever language the Workbench is in. A board the project keeps for good, like the
  deploy example, can take its words from `WorkbenchStrings` instead and leave the language unset, so it reads in the
  Workbench's language.
- Keep it calm: the theme's neutral tokens, and the board's tone as its one accent, for what moves or matters most. No
  color per part, no glows. Motion only where it explains something, like a path being travelled or one version taking
  over from another, and none under `prefers-reduced-motion`.
- Nothing jumps. What the reader picks changes the words on a part, never its size: give such parts fixed heights,
  one-line figures and notes clamped to a line count, and equal heights in a row.
- Follow `AGENTS.md` section 5 as for any page: bit components styled through their parameters and public variables
  (`--bit-Icon-size` and `--bit-Icon-color` size and color an icon), theme tokens in its `.razor.scss`, rows that stack at
  phone width, and nothing that scrolls sideways.
- What the reader changes on it, like a filter, a slider or a tab, lives in a bit Blex store, so a reload keeps it (the
  **workbench** skill, section 2). A new store is registered in
  `src/Workbench/Infrastructure/Extensions/IWorkbenchServiceCollectionExtensions.cs`, startup code that takes one restart.
  A board without controls needs no store.

## 4. Look at it

- Open `/boards/<id>` on the Workbench's own address, then in dark (the Workbench's theme button), at phone width, and
  with the Workbench in a right-to-left language. Take screenshots of each with the Playwright MCP tools.
- Look at every screenshot as its reader would. Findings: a paragraph, jargon on the main path, more than one accent, a
  part that changes size when something is picked, text cut at one width, parts that overlap, a bar not to scale, a color
  outside the theme. Fix what you find in one pass rather than in a loop of looks.
- Read every fact once more against its source.

## 5. Hand it over

- Give the user its address, `/boards/<id>` on the Workbench's own address, and say in a sentence what it shows. Don't
  repeat the board in the chat.
- To show it to someone on another machine, share it the way the **workbench** skill's section 6 says.

## 6. Keep it or let it go

- When the user wants to keep it in the repository, move its catalog entry's title and summary into
  `WorkbenchStrings.resx` in every language the Workbench has, which its tests check, and commit it with the work it
  explains.
- Otherwise delete its folder and its catalog entry once the user is done with it.
- What the user decides from it is a product decision: write it into the feature's spec (`AGENTS.md` section 6).

## Done when

- It answers its question at the top, at a glance, and every fact on it comes from the project or from a source it names
  and dates.
- It reads well in light and dark, at phone and desktop widths and right to left, and nothing on it jumps.
- The user has its address, and what they said about it is done or answered.
