"use client";

import { useActionState } from "react";

import {
  type CreateOrgState,
  createOrganizationAction,
} from "@/app/(app)/settings/organizations/actions";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const INITIAL: CreateOrgState = {};

export function CreateOrganizationForm() {
  const [state, formAction, isPending] = useActionState(createOrganizationAction, INITIAL);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">New workspace</CardTitle>
        <CardDescription>You become its owner.</CardDescription>
      </CardHeader>
      <CardContent>
        <form action={formAction} className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <div className="flex-1 space-y-1.5">
            <Label htmlFor="name">Name</Label>
            <Input id="name" name="name" placeholder="Acme Industries" required />
          </div>
          <Button type="submit" disabled={isPending}>
            {isPending ? "Creating…" : "Create"}
          </Button>
        </form>

        {state.error ? (
          <p role="alert" className="text-destructive mt-3 text-sm">
            {state.error}
          </p>
        ) : null}

        {state.created ? (
          <p className="text-muted-foreground mt-3 text-sm">
            Created {state.created}. Switch to it from the sidebar.
          </p>
        ) : null}
      </CardContent>
    </Card>
  );
}
