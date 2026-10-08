# About the Project

!!! warning "Draft"
    This is a test version of the documentation site, and this page is a draft. Content is basic for now and will grow.

**RISC-V Adventure** is an educational game that teaches how a processor actually works, using the **RISC-V** instruction set architecture as the concrete example.

It's built for people who want more than a textbook diagram of a CPU - the goal is to understand how the bits of an instruction turn into real signals traveling along real wires between real registers, the ALU, and memory.

## Who it's for

Students meeting computer architecture for the first time, and anyone curious what actually happens "under the hood" of a processor, without needing a background in digital logic.

## What's inside

The game is organized as a curriculum, not a single sandbox (but sandbox is planned) - each section builds on the previous one:

- **Individual components** - ALU, register, multiplexer, register file, memory, sign extender - introduced one at a time, in isolation.
- **Single-cycle processor** (*Eintaktprozessor*) - the same components wired together into a complete, working CPU: one instruction, one clock cycle.
- **Multi-cycle processor** (*Mehrtaktprozessor*) - the same hardware reused across several clock cycles per instruction.
- **Pipelined processor** - the most advanced section: several instructions in at once, across different pipeline stages.

See [Concept & Teaching Approach](concept.md) for how each level actually teaches these ideas, and [Technology](tech-stack.md) for how the game is built.

## Where to find it

- [Google Play](https://play.google.com/store/apps/details?id=com.edu.mehrtaktproz.sim) - mobile release
- [GitHub](https://github.com/GGalya1/riscv-architecture-educational-game) - source code, public repository, all platforms
- [Itch.io](https://ggalya.itch.io/riscv-adventure) - all platforms

The project is being developed as part of a Bachelor's thesis (TU Darmstadt).
