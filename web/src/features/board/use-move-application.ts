"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";
import { queries } from "@/lib/api/queries";

type MoveVariables = { id: string; status: string };

/**
 * Cambiar de estado con actualización OPTIMISTA: la tarjeta se mueve al instante y,
 * si la API rechaza la transición (State pattern en el dominio), vuelve a su lugar.
 */
export function useMoveApplication() {
  const queryClient = useQueryClient();
  const listKey = queries.applications.list().queryKey;

  return useMutation({
    mutationFn: ({ id, status }: MoveVariables) =>
      unwrap(api.POST("/api/v1/applications/{id}/status", { params: { path: { id } }, body: { status } })),

    onMutate: async ({ id, status }) => {
      await queryClient.cancelQueries({ queryKey: listKey });
      const previous = queryClient.getQueryData(listKey);

      // setQueryData está tipado gracias a queryOptions: TypeScript conoce la forma de la lista.
      queryClient.setQueryData(listKey, (old) =>
        old && { ...old, items: old.items.map((item) => (item.id === id ? { ...item, status, allowedTransitions: [] } : item)) },
      );

      return { previous };
    },

    onError: (_error, _variables, context) => {
      queryClient.setQueryData(listKey, context?.previous);
    },

    onSuccess: (updated) => {
      queryClient.setQueryData(listKey, (old) =>
        old && {
          ...old,
          items: old.items.map((item) =>
            item.id === updated.id
              ? { ...item, status: updated.status, allowedTransitions: updated.allowedTransitions, updatedAt: updated.updatedAt }
              : item,
          ),
        },
      );
      queryClient.setQueryData(queries.applications.detail(updated.id).queryKey, updated);
    },

    onSettled: () => queryClient.invalidateQueries({ queryKey: queries.applications.all }),
  });
}
