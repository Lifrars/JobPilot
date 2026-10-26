"use client";

import { useRouter } from "next/navigation";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { Button } from "@/components/ui";

export function LogoutButton() {
  const router = useRouter();
  const queryClient = useQueryClient();

  const logout = useMutation({
    mutationFn: () => api.POST("/api/v1/auth/logout"),
    onSettled: () => {
      // Borra la caché del navegador: el próximo usuario no debe ver datos del anterior.
      queryClient.clear();
      router.replace("/login");
      router.refresh();
    },
  });

  return (
    <Button variant="secondary" pending={logout.isPending} onClick={() => logout.mutate()}>
      Salir
    </Button>
  );
}
