import { inject, provide } from "vue";
import { RosterContext } from "./types.partial";

const contextSymbol = Symbol("RosterContext");

export function provideRosterContext(context: RosterContext): void {
    provide(contextSymbol, context);
}

export function useRosterContext(): RosterContext {
    const context = inject<RosterContext>(contextSymbol);

    if (!context) {
        throw new Error("RosterContext is not provided");
    }

    return context;
}
