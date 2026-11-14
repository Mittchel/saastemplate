"""Agent tools.

One worked example, which exists to demonstrate the pattern the rest of a
product's tools should follow: a tool reaches business data by calling the .NET
API with the end user's own access token -- never by querying the database.

That has three consequences worth stating plainly:

* The agent is authorised exactly like a UI click. Permissions, organization
  role and row-level security all apply unchanged, so a tool cannot read
  anything the user could not have read themselves.
* This service holds no credentials for business data. Its database role can
  only reach the `ai` schema (LangGraph checkpoints).
* Tenant isolation does not depend on the agent behaving well. A prompt
  injection that talks the model into asking for another organization's data
  gets a 404 from the API, because the token says which tenant it is.
"""

from langchain_core.tools import StructuredTool

from app.api_client import ApiClient, ApiError


def build_tools(api: ApiClient) -> list[StructuredTool]:
    """Build the tool set bound to one caller's API client."""

    async def get_current_user() -> str:
        """Look up the signed-in user and the organization they are working in.

        Use this when the user asks who they are, which workspace they are in,
        or what their role or permissions are.
        """
        try:
            me = await api.get_me()
        except ApiError as error:
            # Returned to the model rather than raised: a failed tool call is
            # information the agent can act on, and a raise would abort the run.
            return f"Could not read the current user ({error.status_code})."

        organization = me.get("organization", {})

        return (
            f"Name: {me.get('fullName') or me.get('email')}\n"
            f"Email: {me.get('email')}\n"
            f"Organization: {organization.get('name')}"
            f" ({'personal workspace' if organization.get('isPersonal') else 'team workspace'})\n"
            f"Role: {me.get('organizationRole')}"
        )

    return [
        StructuredTool.from_function(
            coroutine=get_current_user,
            name="get_current_user",
            description=(
                "Look up the signed-in user and their current organization, "
                "including their role. Takes no arguments."
            ),
        ),
    ]
